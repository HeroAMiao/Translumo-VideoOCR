#define VIDEO_OCR_PROFILE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using FFMpegCore;
#if VIDEO_OCR_PROFILE
using Microsoft.Extensions.Logging;
#endif
using Translumo.Configuration;
using Translumo.OCR;
using Translumo.OCR.Configuration;
using Translumo.OCR.EasyOCR;
using Translumo.OCR.Tesseract;
using Translumo.OCR.WindowsOCR;
using Translumo.Processing.TextProcessing;
using Translumo.Translation.Configuration;

namespace Translumo.Services
{
    public class DefaultVideoOcrService : IVideoOcrService, IDisposable
    {
        private static readonly List<int> _dpArr = new();
        
        private readonly OcrEnginesFactory _enginesFactory;
        private TranslationConfiguration _translationConfiguration;
        private OcrGeneralConfiguration _ocrGeneralConfiguration;
        private readonly TextDetectionProvider _textProvider;
        private readonly MultiThreadVideoCaptureServiceFactory _videoCaptureServiceFactory;
        private readonly SequenceVideoCaptureServiceFactory _sequenceVideoCaptureServiceFactory;
        private readonly ILogger<DefaultVideoOcrService> _logger;
        
        private const float MIN_SCORE_THRESHOLD = 2.1f;

        public DefaultVideoOcrService(OcrEnginesFactory ocrEnginesFactory,
            TranslationConfiguration translationConfiguration, OcrGeneralConfiguration ocrConfiguration,
            TextDetectionProvider textProvider, MultiThreadVideoCaptureServiceFactory videoCaptureServiceFactory,
            SequenceVideoCaptureServiceFactory sequenceVideoCaptureServiceFactory,
            ILogger<DefaultVideoOcrService> logger)
        {
            _enginesFactory = ocrEnginesFactory;
            _textProvider = textProvider;
            _videoCaptureServiceFactory = videoCaptureServiceFactory;
            _sequenceVideoCaptureServiceFactory = sequenceVideoCaptureServiceFactory;
            _logger = logger;
            _translationConfiguration = translationConfiguration;
            _textProvider.Language = translationConfiguration.TranslateFromLang;
            
        }


        private IEnumerable<IOCREngine> InitializeEngines()
        {
            return _enginesFactory
                .GetEngines(_ocrGeneralConfiguration.OcrConfigurations, _translationConfiguration.TranslateFromLang, true)
                .ToArray();
        }

        public Task Start(VideoOcrConfiguration videoOcrConfiguration, IProgress<float> progress)
        {
            return SequenceExecute(videoOcrConfiguration);
            // return Task.Run(() => ExecuteSync(videoOcrConfiguration, progress));
        }

        private void ReOcrSrtResults(List<SrtEntry> srtEntries, IMultiThreadVideoCaptureService videoCaptureService, int interval)
        {
            
            var engines = _enginesFactory.GetEngines(new OcrConfiguration[]
            {
                new TesseractOCRConfiguration{Enabled = true},
                new WindowsOCRConfiguration {Enabled = false},
                new EasyOCRConfiguration {Enabled = false},
            }, _translationConfiguration.TranslateFromLang);
            
            for (var i = 0; i < srtEntries.Count; i++)
            {
                Console.WriteLine($"ReOcr {i} {srtEntries.Count}");
                var srtEntry = srtEntries[i];
                var timeSpan = srtEntry.End - TimeSpan.FromMilliseconds(interval);
                if (timeSpan < srtEntry.Start)
                {
                    timeSpan = srtEntry.Start;
                }
                var screenshot = videoCaptureService.GetFrameAt(timeSpan);
                var taskResults = engines.Select(engine => _textProvider.GetTextAsync(engine, screenshot)).ToArray();
                Task.WaitAll(taskResults);
                var bestResult = GetBestDetectionResult(taskResults);
                srtEntry.Text = bestResult.Text;
            }
        }

        public async Task SequenceExecute(VideoOcrConfiguration configuration)
        {
            var videoCaptureService = _sequenceVideoCaptureServiceFactory.Create(configuration.VideoPath,
                configuration.Rectangle, configuration.Interval);
            var results = new List<OcrResult>();
            var engines = _enginesFactory.GetEngines(new OcrConfiguration[]
            {
                new TesseractOCRConfiguration
                {
                    Enabled = false
                },
                new WindowsOCRConfiguration
                {
                    Enabled = true
                },
                new EasyOCRConfiguration
                {
                    Enabled = false
                }
            }, _translationConfiguration.TranslateFromLang, true).ToArray();
            var i = 0;
            var interval = TimeSpan.FromMilliseconds(configuration.Interval);
            await videoCaptureService.SequenceProcess(async tiff =>
            {
                var taskResults = engines.Select(engine => _textProvider.GetTextAsync(engine, tiff)).ToArray();
                await Task.WhenAll(taskResults);
                var bestResult = GetBestDetectionResult(taskResults);

                //由于未知原因，FFMPEG生成的第一帧与第二帧是相同的，所以我们跳过第一帧
                if (i <= 0)
                {
                    i++;
                    return;
                }
                var timestamp = Math.Max(0, i - 1) * interval;
                i++;
                if (bestResult.ValidityScore <= MIN_SCORE_THRESHOLD)
                {
                    return;
                }

                Console.WriteLine($"Sequence {i}");
                results.Add(new OcrResult(timestamp, bestResult.Text));
            }).ConfigureAwait(false);
            Console.WriteLine("First Step Finished");
            if (!results.Any())
            {
                Console.WriteLine("No text detected");
                return;
            }
            
            var srtEntries = PostProcessText(results, configuration);
            
            using var captureService = _videoCaptureServiceFactory.GetService(configuration.VideoPath, configuration.Rectangle);
            ReOcrSrtResults(srtEntries, captureService, configuration.Interval);
            OutputSrt(srtEntries, configuration.VideoPath + ".srt");
        }
        

        private void ExecuteSync(VideoOcrConfiguration configuration, IProgress<float> progress)
        {
#if VIDEO_OCR_PROFILE
            var ffmpegStopWatch = new Stopwatch();
            var ocrStopWatch = new Stopwatch();
            var totalStopWatch = Stopwatch.StartNew();
#endif
            var results = new List<OcrResult>();
            var videoInfo = FFProbe.Analyse(configuration.VideoPath);
            var duration = videoInfo.Duration;
            var interval = TimeSpan.FromMilliseconds(configuration.Interval);
            var frameCount = (int)Math.Round(duration / interval);
            var engines = _enginesFactory.GetEngines(new OcrConfiguration[]
            {
                new TesseractOCRConfiguration
                {
                    Enabled = false
                },
                new WindowsOCRConfiguration
                {
                    Enabled = true
                },
                new EasyOCRConfiguration
                {
                    Enabled = false
                }
            }, _translationConfiguration.TranslateFromLang, true).ToArray();
            using var captureService = _videoCaptureServiceFactory.GetService(configuration.VideoPath, configuration.Rectangle);
            for (var i = 0; i < frameCount; i++)
            {
                progress.Report((float) i / frameCount);
                var captureTime = i * interval;
                Console.WriteLine($"{i}/{frameCount}");
#if VIDEO_OCR_PROFILE
                ffmpegStopWatch.Start();
#endif
                var screenshot = captureService.GetFrameAt(captureTime);
#if VIDEO_OCR_PROFILE
                ffmpegStopWatch.Stop();
                ocrStopWatch.Start();
#endif
                var taskResults = engines.Select(engine => _textProvider.GetTextAsync(engine, screenshot)).ToArray();
                Task.WaitAll(taskResults);
                var bestResult = GetBestDetectionResult(taskResults);
#if VIDEO_OCR_PROFILE
                ocrStopWatch.Stop();
#endif
                if (bestResult.ValidityScore <= MIN_SCORE_THRESHOLD)
                {
                    continue;
                }
                
                results.Add(new OcrResult(captureTime, bestResult.Text));
            }

            if (!results.Any())
            {
                _logger.LogWarning("No text detected.");
                return;
            }

            using (var store = File.OpenWrite("store.xml"))
            {
                new XmlSerializer(typeof(List<Store>)).Serialize(store, results.Select(Store.Create).ToList());
            }
            var srtEntries = PostProcessText(results, configuration);
            
            var reOcrWatch = Stopwatch.StartNew();
            ReOcrSrtResults(srtEntries, captureService, configuration.Interval);
            reOcrWatch.Stop();
            OutputSrt(srtEntries, configuration.VideoPath + ".srt");
            
            
#if VIDEO_OCR_PROFILE
            
            totalStopWatch.Stop();
            _logger.LogInformation(
                "Profiler finished in {totalSeconds} seconds. FFMPEG {ffmpegSeconds} seconds, OCR {ocrSeconds} seconds. ReOCR {reOcrSeconds} seconds.",
                totalStopWatch.Elapsed.TotalSeconds, ffmpegStopWatch.Elapsed.TotalSeconds,
                ocrStopWatch.Elapsed.TotalSeconds, reOcrWatch.Elapsed.TotalSeconds);
#endif
        }

        private void OutputSrt(List<SrtEntry> srtEntries, string path)
        {
            using var writer = new StreamWriter(File.OpenWrite(path));
            for (var i = 0; i < srtEntries.Count; i++)
            {
                var entry = srtEntries[i];
                writer.WriteLine(i + 1);
                writer.Write(entry.Start.ToString(@"hh\:mm\:ss\,fff"));
                writer.Write(" --> ");
                writer.WriteLine(entry.End.ToString(@"hh\:mm\:ss\,fff"));
                writer.WriteLine(entry.Text);
                writer.WriteLine();
            }
        }
        
        private static int LongestCommonSubsequence(string text1, string text2)
        {
            _dpArr.Clear();
            var totalLength = (text1.Length + 1) * (text2.Length + 1);
            _dpArr.EnsureCapacity(totalLength);
            for (var i = 0; i < totalLength; i++)
            {
                _dpArr.Add(0);
            }

            for (var i = 1; i <= text1.Length; i++)
            {
                for (var j = 1; j <= text2.Length; j++)
                {
                    if (text1[i - 1] == text2[j - 1])
                    {
                        _dpArr[i * text2.Length + j] = _dpArr[(i - 1) * text2.Length + j - 1] + 1;
                    }
                    else
                    {
                        _dpArr[i * text2.Length + j] = Math.Max(_dpArr[(i - 1) * text2.Length + j], _dpArr[i * text2.Length + j - 1]);
                    }
                }
            }

            return _dpArr[text1.Length * text2.Length + text2.Length];
        }

        private enum State
        {
            Init, DeterminingLength, CheckingStable
        }

        private bool IsSameText(string prevText, string currentText, VideoOcrConfiguration configuration)
        {
            var common = LongestCommonSubsequence(prevText, currentText);
            //Due to typewriting effect, prev text should have the smaller length, so we use prev text's length as
            //reference length. And if currentText's length is less than prevText dramatically, then the text is likely
            //changed.   
            var prevLength = prevText.Length;
            var diffCharCount = prevLength - common;
            var maxDiffCharCount = Math.Ceiling(configuration.ChangeThreshold * prevLength);
            return diffCharCount <= maxDiffCharCount;
        }
        
        private List<SrtEntry> PostProcessText(List<OcrResult> texts, VideoOcrConfiguration ocrConfiguration)
        {
            var state = State.Init;
            var prevState = State.Init;
            var checking = texts[0];
            
            OcrResult stableTrackingCurrent = default;
            var stableTrackingCount = 0;
            OcrResult stableTrackingStart = default;
            
            OcrResult lengthDeterminationStart = default;
            OcrResult lengthDeterminationCurrent = default;
            
            List<SrtEntry> srtEntries = new();
            
            for (var i = 0; i < texts.Count; i++)
            {
                var result = texts[i];
                switch (state)
                {
                    case State.Init:
                        prevState = State.Init;
                        stableTrackingStart = result;
                        stableTrackingCurrent = result;
                        stableTrackingCount = 0;
                        state = State.CheckingStable;
                        break;
                    case State.CheckingStable:
                        var isSameText = IsSameText(stableTrackingCurrent.Text, result.Text, ocrConfiguration);
                        Console.WriteLine($"Checking Stable {i} {result.Time.Ticks} {result.Time} {stableTrackingCurrent.Text} {result.Text} {isSameText} {stableTrackingCount}");
                        if (!isSameText)
                        {
                            state = prevState;
                            i--;
                        }
                        else
                        {
                            stableTrackingCurrent = result;
                            stableTrackingCount++;
                            if (stableTrackingCount >= ocrConfiguration.StableFrameCount)
                            {
                                if (prevState == State.DeterminingLength)
                                {
                                    srtEntries.Add(new SrtEntry(lengthDeterminationStart.Time, lengthDeterminationCurrent.Time, lengthDeterminationCurrent.Text));
                                }
                                prevState = state;
                                lengthDeterminationStart = stableTrackingStart;
                                lengthDeterminationCurrent = result;
                                state = State.DeterminingLength;
                            }
                        }
                        break;
                    case State.DeterminingLength:
                        var sameText = IsSameText(lengthDeterminationCurrent.Text, result.Text, ocrConfiguration);
                        Console.WriteLine($"DeterminingLength {i} {result.Time.Ticks} {result.Time} {lengthDeterminationCurrent.Text} {result.Text} {sameText}");
                        if (sameText)
                        {
                            lengthDeterminationCurrent = result;
                        }
                        else
                        {
                            prevState = state;
                            stableTrackingStart = result;
                            stableTrackingCurrent = result;
                            stableTrackingCount = 0;
                            state = State.CheckingStable;
                        }
                        break;
                }
            }

            if (state == State.DeterminingLength || prevState == State.DeterminingLength)
            {
                srtEntries.Add(new SrtEntry(lengthDeterminationStart.Time, lengthDeterminationCurrent.Time, lengthDeterminationCurrent.Text));
            }

            return srtEntries;
        }
        
        
        private TextDetectionResult GetBestDetectionResult(Task<TextDetectionResult>[] results, int minCountSameResults = 3)
        {
            var maxScoreIndex = 0;
            for (var i = 0; i < results.Length; i++)
            {
                maxScoreIndex = results[maxScoreIndex].Result.CompareTo(results[i].Result) > 0 ? maxScoreIndex : i;
                if (i > results.Length - minCountSameResults || results[i].Result.ValidityScore == 0)
                {
                    continue;
                }

                var intRowCount = 1;
                var inRowIndex = i;
                for (var j = i + 1; j < results.Length; j++)
                {
                    if (results[i].Result.ValidatedText == results[j].Result.ValidatedText)
                    {
                        intRowCount++;
                        inRowIndex = results[inRowIndex].Result.SourceEngine.Confidence > results[j].Result.SourceEngine.Confidence
                            ? inRowIndex
                            : j;
                    }
                }
                //If array contains multiple (=minCountSameResults) same results, consider it as the best
                if (intRowCount >= minCountSameResults)
                {
                    results[inRowIndex].Result.ValidityScore = float.MaxValue;
                    return results[inRowIndex].Result;
                }
            }

            return results[maxScoreIndex].Result;
        }

        public void Dispose()
        {
            _textProvider?.Dispose();
        }

        public class Store
        {
            public long Tick { get; set; }
            public string Name { get; set; }

            public OcrResult ToResult()
            {
                return new OcrResult(TimeSpan.FromTicks(Tick), Name);
            }

            public static Store Create(OcrResult result)
            {
                return new Store
                {
                    Tick = result.Time.Ticks,
                    Name = result.Text
                };
            }
            
            
        }

        private class SrtEntry
        {
            public TimeSpan Start;
            public TimeSpan End;
            public string Text;

            public SrtEntry(TimeSpan start, TimeSpan end, string text)
            {
                Start = start;
                End = end;
                Text = text;
            }
        }

        public struct OcrResult
        {
            public readonly TimeSpan Time;
            public readonly string Text;

            public OcrResult(TimeSpan time, string text)
            {
                Time = time;
                Text = text;
            }
        }
    }
}