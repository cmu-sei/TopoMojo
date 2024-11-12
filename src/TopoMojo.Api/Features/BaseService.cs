// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMapper;

namespace TopoMojo.Api.Services
{
    public abstract class BaseService
    {
        public BaseService(
            ILogger logger,
            IMapper mapper,
            CoreOptions options
        )
        {
            Logger = logger;
            CoreOptions = options;
            Mapper = mapper;

            JsonOptions = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            JsonOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            );
        }

        protected IMapper Mapper { get; }
        protected ILogger Logger { get; }
        protected CoreOptions CoreOptions { get; }
        protected JsonSerializerOptions JsonOptions { get; }

    }
}
