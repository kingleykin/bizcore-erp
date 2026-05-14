namespace Bizcore.BuildingBlocks.Exceptions
{
    public class NotFoundException : Exception
    {
        public string Code { get; }
        public object? Parameters { get; }

        public NotFoundException(string code, string? message = null, object? parameters = null) 
            : base(message ?? code)
        {
            Code = code;
            Parameters = parameters;
        }

        public NotFoundException(string resource, object id)
            : base($"{resource} with id '{id}' was not found.")
        {
            Code = ErrorCodes.Common.NotFound;
            Parameters = new { Resource = resource, Id = id };
        }
    }
}
