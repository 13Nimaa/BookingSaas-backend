namespace BookingSaas_backend.Application.Exceptions;

public class ImageUploadException : Exception
{
    public ImageUploadException(string message)
        : base(message)
    {
    }
}