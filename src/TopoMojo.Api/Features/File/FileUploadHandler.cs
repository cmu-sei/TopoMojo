// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Collections.Specialized;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace TopoMojo.Api.Services
{
    public interface IFileUploadHandler
    {
        Task Process(
            HttpRequest request,
            Func<NameValueCollection, Stream> getDestinationStream,
            Action<FileUploadStatus> statusUpdate,
            Action<FormOptions> optionsAction,
            Action<NameValueCollection> postProcess
        );
    }

    public class FileUploadHandler(
        ILogger<FileUploadHandler> logger
        ) : IFileUploadHandler
    {
        readonly FormOptions _formOptions = new();
        readonly ILogger<FileUploadHandler> _logger = logger;

        public async Task Process(
            HttpRequest request,
            Func<NameValueCollection, Stream> getDestinationStream,
            Action<FileUploadStatus> statusUpdate,
            Action<FormOptions> optionsAction,
            Action<NameValueCollection> postProcess = null
        )
        {
            if (!request.ContentType.IsMultipartContentType())
                throw new InvalidOperationException($"Expected a multipart request, but got {request.ContentType}");

            optionsAction?.Invoke(_formOptions);

            string boundary = request.ContentType.GetBoundary();

            NameValueCollection metadata = [];
            MultipartReader reader = new(boundary, request.Body);
            MultipartSection section = await reader.ReadNextSectionAsync();
            while (section != null)
            {
                bool hasContentDispositionHeader = ContentDispositionHeaderValue
                    .TryParse(section.ContentDisposition, out ContentDispositionHeaderValue contentDisposition);

                if (hasContentDispositionHeader)
                {
                    if (contentDisposition.HasFormDataContentDisposition())
                    {
                        //add form values to metadata collection
                        var encoding = section.GetEncoding();
                        using var streamReader = new StreamReader(
                            section.Body,
                            encoding,
                            detectEncodingFromByteOrderMarks: true,
                            bufferSize: 1024,
                            leaveOpen: true
                        );
                        // The value length limit is enforced by MultipartBodyLengthLimit
                        string value = await streamReader.ReadToEndAsync();
                        if (string.Equals(value, "undefined", StringComparison.OrdinalIgnoreCase))
                        {
                            value = string.Empty;
                        }
                        metadata = value.ParseFormValues();

                    }

                    if (contentDisposition.HasFileContentDisposition())
                    {
                        metadata.Add("original-name", HeaderUtilities.RemoveQuotes(contentDisposition.FileName).Value);

                        try
                        {
                            Stream targetStream = getDestinationStream.Invoke(metadata);
                            FileUploadStatus status = new()
                            {
                                Metadata = metadata,
                                Key = metadata["monitor-key"] ?? $"{metadata["group-key"]}-{metadata["original-name"]}",
                                Size = long.Parse(metadata["size"] ?? "1E9")
                            };

                            await Save(section.Body, targetStream, status, statusUpdate);

                            postProcess?.Invoke(metadata);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception("Error", ex);
                        }
                    }
                }

                // Drains any remaining section body that has not been consumed and
                // reads the headers for the next section.
                section = await reader.ReadNextSectionAsync();
            }
        }

        private async Task Save(Stream source, Stream dest, FileUploadStatus status, Action<FileUploadStatus> statusUpdate)
        {
            try
            {
                status.StartedAt = DateTimeOffset.UtcNow;
                statusUpdate?.Invoke(status);

                byte[] buffer = new byte[4096];
                int bytes = 0;
                long totalBlocks = 0;

                do
                {
                    bytes = await source.ReadAsync(buffer);
                    await dest.WriteAsync(buffer.AsMemory(0, bytes));
                    totalBlocks += 1;
                    status.Count += bytes;
                    if (totalBlocks % 1024 == 0 && statusUpdate != null)
                    {
                        statusUpdate.Invoke(status);
                    }
                } while (bytes > 0);

                status.Count = status.Size;
                status.StoppedAt = DateTimeOffset.UtcNow;
                _logger.LogInformation("File upload complete: {key} {count}b {duration}s {rate}b/s", status.Key, status.Count, status.Duration, status.Rate);
            }
            catch (Exception ex)
            {
                status.Error = ex;
                _logger.LogError(0, ex, "File upload failed: {key}", status.Key);
            }
            finally
            {
                await dest.FlushAsync();
                dest.Dispose();
                statusUpdate.Invoke(status);  //give caller chance to clean up
            }
        }
    }

    public class FileUploadStatus
    {
        public NameValueCollection Metadata { get; set; }
        public string Key { get; set; }
        public long Size { get; set; }
        public long Count { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset StoppedAt { get; set; }
        public Exception Error { get; set; }
        public int Progress => (int)(Count / (float)Size * 100);
        public int Duration => (int)StoppedAt.Subtract(StartedAt).TotalSeconds;
        public int Rate
        {
            get
            {
                DateTimeOffset now = (StoppedAt > DateTimeOffset.MinValue) ? StoppedAt : DateTimeOffset.UtcNow;
                return Convert.ToInt32(Count / now.Subtract(StartedAt).TotalSeconds);
            }
        }
    }
}
