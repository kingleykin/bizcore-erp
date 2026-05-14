using System;

namespace Bizcore.BuildingBlocks.Exceptions
{
    public class DomainException : Exception
    {
        public string Code { get; }
        public object? Parameters { get; }

        public DomainException(string code, string? message = null, object? parameters = null) 
            : base(message ?? code)
        {
            Code = code;
            Parameters = parameters;
        }

        public DomainException(string code, string message, Exception innerException, object? parameters = null) 
            : base(message, innerException)
        {
            Code = code;
            Parameters = parameters;
        }
    }
}
