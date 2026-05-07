namespace Bizcore.BuildingBlocks.Exceptions
{
    public class NotFoundException : Exception
    {
        public NotFoundException(string resource, object id)
            : base($"{resource} with id '{id}' was not found.")
        {
        }

        public NotFoundException(string message) : base(message)
        {
        }
    }
}
