﻿using System;
using System.Net.Http;
using Raven.NewClient.Abstractions.Util;
using Raven.NewClient.Client.Blittable;
using Raven.NewClient.Client.Commands;
using Raven.NewClient.Client.Data;
using Raven.NewClient.Client.Document;
using Raven.NewClient.Client.Http;
using Raven.NewClient.Client.Json;
using Sparrow.Json;

namespace Raven.NewClient.Operations.Databases.Documents
{
    public class PatchByCollectionOperation : IOperation
    {
        private readonly string _collectionName;
        private readonly PatchRequest _patch;

        public PatchByCollectionOperation(string collectionName, PatchRequest patch)
        {
            if (collectionName == null)
                throw new ArgumentNullException(nameof(collectionName));
            if (patch == null)
                throw new ArgumentNullException(nameof(patch));

            _collectionName = collectionName;
            _patch = patch;
        }

        public RavenCommand<OperationIdResult> GetCommand(DocumentConvention conventions, JsonOperationContext context)
        {
            return new PatchByCollectionCommand(conventions, context, _collectionName, _patch);
        }

        private class PatchByCollectionCommand : RavenCommand<OperationIdResult>
        {
            private readonly JsonOperationContext _context;
            private readonly string _collectionName;
            private readonly BlittableJsonReaderObject _patch;

            public PatchByCollectionCommand(DocumentConvention conventions, JsonOperationContext context, string collectionName, PatchRequest patch)
            {
                if (conventions == null)
                    throw new ArgumentNullException(nameof(conventions));
                if (context == null)
                    throw new ArgumentNullException(nameof(context));
                if (collectionName == null)
                    throw new ArgumentNullException(nameof(collectionName));
                if (patch == null)
                    throw new ArgumentNullException(nameof(patch));

                _context = context;
                _collectionName = collectionName;
                _patch = new EntityToBlittable(null).ConvertEntityToBlittable(patch, conventions, _context);
            }

            public override HttpRequestMessage CreateRequest(ServerNode node, out string url)
            {
                url = $"{node.Url}/databases/{node.Database}/collections/docs?name={_collectionName}";
                IsReadRequest = false;

                var request = new HttpRequestMessage
                {
                    Method = HttpMethods.Patch,
                    Content = new BlittableJsonContent(stream =>
                    {
                        _context.Write(stream, _patch);
                    })
                };

                return request;
            }

            public override void SetResponse(BlittableJsonReaderObject response)
            {
                if (response == null)
                    ThrowInvalidResponse();

                Result = JsonDeserializationClient.OperationIdResult(response);
            }
        }
    }
}