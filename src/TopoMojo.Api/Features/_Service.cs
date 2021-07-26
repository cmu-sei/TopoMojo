// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMapper;
using Microsoft.Extensions.Logging;

namespace TopoMojo.Api.Services
{
    public abstract class _Service
    {
        public _Service(
            ILogger logger,
            IMapper mapper,
            CoreOptions options
        )
        {
            _logger = logger;
            _options = options;
            Mapper = mapper;

            jsonOptions = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            jsonOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            );
        }

        protected IMapper Mapper { get; }
        protected ILogger _logger { get; }
        protected CoreOptions _options { get; }
        protected JsonSerializerOptions jsonOptions { get; }

    }
}
