namespace File.API.Application.DTOs;

public record FileUploadResponse(string FileName, string? Url = null);

public record FileUrlResponse(string Url);
