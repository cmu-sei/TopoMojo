// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace TopoMojo.Api.Services
{
    public static class MultipartRequestExtensions
    {
        // Content-Type: multipart/form-data; boundary="----WebKitFormBoundarymx2fSWqWSd0OxQqq"
        // The spec says 70 characters is a reasonable limit.
        public static string GetBoundary(this string header)
        {
            var boundary = HeaderUtilities.RemoveQuotes(MediaTypeHeaderValue.Parse(header).Boundary);
            if (string.IsNullOrWhiteSpace(boundary.Value))
            {
                throw new InvalidDataException("Missing content-type boundary.");
            }

            // if (boundary.Length > lengthLimit)
            // {
            //     throw new InvalidDataException(
            //         $"Multipart boundary length limit {lengthLimit} exceeded.");
            // }

            return boundary.Value;
        }

        public static bool IsMultipartContentType(this string header)
        {
            return !string.IsNullOrEmpty(header)
                   && header.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool HasFormDataContentDisposition(this ContentDispositionHeaderValue contentDisposition)
        {
            // Content-Disposition: form-data; name="key";
            return contentDisposition != null
                   && contentDisposition.DispositionType.Equals("form-data")
                   && string.IsNullOrEmpty(contentDisposition.FileName.Value)
                   && string.IsNullOrEmpty(contentDisposition.FileNameStar.Value);
        }

        public static bool HasFileContentDisposition(this ContentDispositionHeaderValue contentDisposition)
        {
            // Content-Disposition: form-data; name="myfile1"; filename="Misc 002.jpg"
            return contentDisposition != null
                   && contentDisposition.DispositionType.Equals("form-data")
                   && (!string.IsNullOrEmpty(contentDisposition.FileName.Value)
                       || !string.IsNullOrEmpty(contentDisposition.FileNameStar.Value));
        }

        public static NameValueCollection ParseFormValues(this string querystring)
        {
            string input = HeaderUtilities.RemoveQuotes(querystring).Value;
            NameValueCollection props = new NameValueCollection();
            foreach (string field in input.Split('&'))
            {
                string[] prop = field.Split('=');
                string key = prop[0].Trim();
                string val = (prop.Length > 1) ? prop[1].Trim() : "";
                if (!String.IsNullOrEmpty(key))
                    props.Add(key, val);
            }
            return props;
        }

        public static Encoding GetEncoding(this MultipartSection section)
        {
            var hasMediaTypeHeader = MediaTypeHeaderValue.TryParse(section.ContentType, out MediaTypeHeaderValue mediaType);
            // UTF-7 is insecure and should not be honored. UTF-8 will succeed in
            // most cases.
            if (!hasMediaTypeHeader || Encoding.UTF7.Equals(mediaType.Encoding))
            {
                return Encoding.UTF8;
            }
            return mediaType.Encoding;
        }
    }

}
