namespace Translumo.Utils;

public struct VideoOcrProgress
{

    public int Pass { get; set; }
    public int Current { get; set; }
    public int Max { get; set; }

    public VideoOcrProgress(int pass, int current, int max)
    {
        Pass = pass;
        Current = current;
        Max = max;
    }
}