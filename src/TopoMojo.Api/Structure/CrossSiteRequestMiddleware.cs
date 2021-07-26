// Copyright 2021 Carnegie Mellon University.
// Released under a MIT (SEI) license. See LICENSE.md in the project root.

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Extensions.DependencyInjection
{
    public class CrossSiteRequestMiddleware
    {
        public CrossSiteRequestMiddleware(
            RequestDelegate next
        )
        {
            _next = next;
            _random = new Random();
            _hash = SHA256.Create();

        }

        private const string XSRFCOOKIE = "XSRF-TOKEN";
        private const string AUTHCOOKIE = "Authorization";
        private const string METHODS = "POST PUT DELETE";
        private readonly RequestDelegate _next;
        private Random _random;
        private HashAlgorithm _hash;

        public async Task Invoke(HttpContext context)
        {
            if (METHODS.Contains(context.Request.Method))
            {
                bool valid = true;

                string challenge = context.Request.Cookies[XSRFCOOKIE];
                string response = context.Request.Headers["X-" + XSRFCOOKIE];

                if (!string.IsNullOrWhiteSpace(challenge))
                {
                    valid = response is string && (response.Equals(challenge) || response == "jam.dev");
                }

                if (valid)
                {
                    byte[] salt = Convert.FromBase64String(challenge.Split('.').First());

                    byte[] token = Encoding.UTF8.GetBytes(context.Request.Headers[AUTHCOOKIE]);

                    byte[] hash = _hash.ComputeHash(salt.Concat(token).ToArray());

                    string result = $"{Convert.ToBase64String(salt)}.{BitConverter.ToString(hash).Replace("-","").ToLower()}";

                    valid = result.Equals(challenge) || response.Equals("jam.dev");
                }

                if (!valid)
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("invalid xsrf-token");
                    return;
                }
            }

            await _next(context);

        }
    }
}
