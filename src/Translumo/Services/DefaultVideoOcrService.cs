#define VIDEO_OCR_PROFILE
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using FFMpegCore;
#if VIDEO_OCR_PROFILE
using Microsoft.Extensions.Logging;
#endif
using Translumo.Configuration;
using Translumo.OCR;
using Translumo.OCR.Configuration;
using Translumo.Processing.TextProcessing;
using Translumo.Translation.Configuration;

namespace Translumo.Services
{
    public class DefaultVideoOcrService : IVideoOcrService, IDisposable
    {
        private readonly OcrEnginesFactory _enginesFactory;
        private TranslationConfiguration _translationConfiguration;
        private OcrGeneralConfiguration _ocrGeneralConfiguration;
        private readonly TextDetectionProvider _textProvider;
        private readonly MultiThreadVideoCaptureServiceFactory _videoCaptureServiceFactory;
        private readonly ILogger<DefaultVideoOcrService> _logger;
        private IOCREngine[] _engines;
        
        private const float MIN_SCORE_THRESHOLD = 2.1f;

        public DefaultVideoOcrService(OcrEnginesFactory ocrEnginesFactory,
            TranslationConfiguration translationConfiguration, OcrGeneralConfiguration ocrConfiguration,
            TextDetectionProvider textProvider, MultiThreadVideoCaptureServiceFactory videoCaptureServiceFactory, ILogger<DefaultVideoOcrService> logger)
        {
            _enginesFactory = ocrEnginesFactory;
            _ocrGeneralConfiguration = ocrConfiguration;
            _textProvider = textProvider;
            _videoCaptureServiceFactory = videoCaptureServiceFactory;
            _logger = logger;
            _translationConfiguration = translationConfiguration;
            _engines = InitializeEngines().ToArray();
            _textProvider.Language = translationConfiguration.TranslateFromLang;
            
            _ocrGeneralConfiguration.PropertyChanged += OcrGeneralConfigurationOnPropertyChanged;
        }

        private void OcrGeneralConfigurationOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            _engines = InitializeEngines().ToArray();
        }

        private IEnumerable<IOCREngine> InitializeEngines()
        {
            return _enginesFactory
                .GetEngines(_ocrGeneralConfiguration.OcrConfigurations, _translationConfiguration.TranslateFromLang, true)
                .ToArray();
        }

        public Task Start(VideoOcrConfiguration videoOcrConfiguration, IProgress<float> progress)
        {
            if (!_engines.Any())
            {
                return Task.FromException(new RuntimeException("no ocr engine is selected."));
            }
            return Task.Run(() => ExecuteSync(videoOcrConfiguration, progress));
        }

        private void ExecuteSync(VideoOcrConfiguration configuration, IProgress<float> progress)
        {
#if VIDEO_OCR_PROFILE
            var ffmpegStopWatch = new Stopwatch();
            var ocrStopWatch = new Stopwatch();
            var totalStopWatch = Stopwatch.StartNew();
#endif
            var sb = new StringBuilder();
            var result = FFProbe.Analyse(configuration.VideoPath);
            var duration = result.Duration;
            var interval = TimeSpan.FromMilliseconds(configuration.Interval);
            var frameCount = (int)Math.Round(duration / interval);
            
            using var captureService = _videoCaptureServiceFactory.GetService(configuration.VideoPath, configuration.Rectangle);
            for (var i = 0; i < frameCount; i++)
            {
                progress.Report((float) i / frameCount);
                var captureTime = i * interval;
#if VIDEO_OCR_PROFILE
                ffmpegStopWatch.Start();
#endif
                var screenshot = captureService.GetFrameAt(captureTime);
#if VIDEO_OCR_PROFILE
                ffmpegStopWatch.Stop();
                ocrStopWatch.Start();
#endif
                var taskResults = _engines.Select(engine => _textProvider.GetTextAsync(engine, screenshot)).ToArray();
                Task.WaitAll(taskResults);
                var bestResult = GetBestDetectionResult(taskResults);
#if VIDEO_OCR_PROFILE
                ocrStopWatch.Stop();
#endif
                if (bestResult.ValidityScore <= MIN_SCORE_THRESHOLD)
                {
                    continue;
                }
            
                sb.AppendLine($"{captureTime}: {bestResult.Text}");
            }
            
            File.WriteAllText("out.txt", sb.ToString());
#if VIDEO_OCR_PROFILE
            totalStopWatch.Stop();
            _logger.LogInformation(
                "Profiler finished in {totalSeconds} seconds. FFMPEG {ffmpegSeconds} seconds, OCR {ocrSeconds} seconds.",
                totalStopWatch.Elapsed.TotalSeconds, ffmpegStopWatch.Elapsed.TotalSeconds,
                ocrStopWatch.Elapsed.TotalSeconds);
#endif
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
    }
}