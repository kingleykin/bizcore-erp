namespace Bizcore.BuildingBlocks.Exceptions
{
    public class ServiceUnavailableException : DomainException
    {
        public ServiceUnavailableException(string message) 
            : base(ErrorCodes.Common.ServiceUnavailable, message)
        {
        }

        public ServiceUnavailableException(string message, Exception innerException) 
            : base(ErrorCodes.Common.ServiceUnavailable, message, innerException)
        {
        }
    }
}
