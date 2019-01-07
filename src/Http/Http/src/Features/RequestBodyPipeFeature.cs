// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO.Pipelines;

namespace Microsoft.AspNetCore.Http.Features
{
    public class RequestBodyPipeFeature : IRequestBodyPipeFeature
    {
        private PipeReader _pipeReader;
        private HttpContext _context;

        public RequestBodyPipeFeature(HttpContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            _context = context;
        }

        public PipeReader RequestBodyPipe
        {
            get
            {
                if (_pipeReader == null ||
                    (_pipeReader is StreamPipeReader reader && !object.ReferenceEquals(reader.InnerStream, _context.Request.Body)))
                {
                    var streamPipeReader = new StreamPipeReader(_context.Request.Body);
                    _pipeReader = streamPipeReader;
                    _context.Response.RegisterForDispose(streamPipeReader);
                }

                return _pipeReader;
            }
            set
            {
                _pipeReader = value ?? throw new ArgumentNullException(nameof(value));
                // TODO set the request body Stream to an adapted pipe https://github.com/aspnet/AspNetCore/issues/3971
            }
        }
    }
}
