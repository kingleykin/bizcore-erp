using System;

namespace Bizcore.BuildingBlocks.Exceptions
{
    public class DomainException : Exception
    {
        public DomainException(string message) : base(message)
        {
        }
    }
}
