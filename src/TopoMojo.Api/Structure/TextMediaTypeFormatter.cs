// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace TopoMojo.Api
{
    public class TextMediaTypeFormatter : IInputFormatter
    {
        public bool CanRead(InputFormatterContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var contentType = context.HttpContext.Request.ContentType;
            if (contentType == null || contentType == "text/plain")
                return true;
            return false;
        }

        public Task<InputFormatterResult> ReadAsync(InputFormatterContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var request = context.HttpContext.Request;
            if (request.ContentLength == 0)
            {
                if (context.ModelType.GetTypeInfo().IsValueType)
                    return InputFormatterResult.SuccessAsync(Activator.CreateInstance(context.ModelType));
                else return InputFormatterResult.SuccessAsync(null);
            }

            using var reader = new StreamReader(context.HttpContext.Request.Body);
            var model = reader.ReadToEndAsync().Result;
            return InputFormatterResult.SuccessAsync(model);
        }
    }
}
