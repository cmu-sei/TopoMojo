// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
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

    public class FileUploadHandler : IFileUploadHandler
    {

        public FileUploadHandler(
            ILogger<FileUploadHandler> logger
        )
        {
            _logger = logger;
            _formOptions = new FormOptions();
        }

        FormOptions _formOptions;
        private readonly ILogger<FileUploadHandler> _logger;

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

            if (optionsAction != null)
                optionsAction.Invoke(_formOptions);

            string boundary = request.ContentType.GetBoundary();

            NameValueCollection metadata = new NameValueCollection();
            MultipartReader reader = new MultipartReader(boundary, request.Body);
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
                        using (var streamReader = new StreamReader(
                            section.Body,
                            encoding,
                            detectEncodingFromByteOrderMarks: true,
                            bufferSize: 1024,
                            leaveOpen: true))
                        {
                            // The value length limit is enforced by MultipartBodyLengthLimit
                            string value = await streamReader.ReadToEndAsync();
                            if (String.Equals(value, "undefined", StringComparison.OrdinalIgnoreCase))
                            {
                                value = String.Empty;
                            }
                            metadata = value.ParseFormValues();
                        }

                    }

                    if (contentDisposition.HasFileContentDisposition())
                    {
                        metadata.Add("original-name", HeaderUtilities.RemoveQuotes(contentDisposition.FileName).Value);

                        try
                        {
                            Stream targetStream = getDestinationStream.Invoke(metadata);
                            FileUploadStatus status = new FileUploadStatus
                            {
                                Metadata = metadata,
                                Key = metadata["monitor-key"] ?? $"{metadata["group-key"]}-{metadata["original-name"]}",
                                Size = Int64.Parse(metadata["size"] ?? "1E9")
                            };

                            await Save(section.Body, targetStream, status, statusUpdate);

                            if (postProcess != null)
                                postProcess.Invoke(metadata);
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
                if (statusUpdate != null)
                    statusUpdate.Invoke(status);

                byte[] buffer = new byte[4096];
                int bytes = 0;
                long totalBlocks = 0;

                do
                {
                    bytes = await source.ReadAsync(buffer, 0, buffer.Length);
                    await dest.WriteAsync(buffer, 0, bytes);
                    totalBlocks += 1;
                    status.Count += bytes;
                    if (totalBlocks % 1024 == 0 && statusUpdate != null)
                    {
                        statusUpdate.Invoke(status);
                    }
                } while (bytes > 0);

                status.Count = status.Size;
                status.StoppedAt = DateTimeOffset.UtcNow;
                _logger.LogInformation("File upload complete: {0} {1}b {2}s {3}b/s", status.Key, status.Count, status.Duration, status.Rate);
            }
            catch (Exception ex)
            {
                status.Error = ex;
                _logger.LogError(0, ex, "File upload failed: {0}", status.Key);
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
        public NameValueCollection Metadata { get; set;}
        public string Key { get; set; }
        public long Size { get; set; }
        public long Count { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset StoppedAt { get; set; }
        public Exception Error { get; set; }
        public int Progress {
            get
            {
                return (int)(((float)Count / (float)Size) * 100);
            }
        }
        public int Duration {
            get
            {
                return (int)StoppedAt.Subtract(StartedAt).TotalSeconds;
            }
        }
        public int Rate {
            get
            {
                DateTimeOffset now = (StoppedAt > DateTimeOffset.MinValue) ? StoppedAt : DateTimeOffset.UtcNow;
                return Convert.ToInt32(Count / now.Subtract(StartedAt).TotalSeconds);
            }
        }
    }
}
