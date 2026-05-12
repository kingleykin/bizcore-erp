namespace Bizcore.BuildingBlocks.Exceptions
{
    public class ServiceUnavailableException : DomainException
    {
        public ServiceUnavailableException(string message) : base(message)
        {
        }

        public ServiceUnavailableException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}
