namespace Translumo.Video
{
    public class TranslumoVideoException : Exception
    {
        public TranslumoVideoException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}