﻿// /* ============================================================================
// Copyright 2014 Hewlett Packard
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ============================================================================ */

using System.Linq;

namespace Openstack.Identity
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Openstack.Common;
    using Openstack.Common.ServiceLocation;

    /// <inheritdoc/>
    internal class IdentityServicePocoClient : IIdentityServicePocoClient
    {
        internal IOpenstackCredential credential;
        internal CancellationToken cancellationToken;
        internal const string IdentityServiceName = "Identity";

        /// <summary>
        /// Creates a new instance of the IdentityServicePocoClient class.
        /// </summary>
        /// <param name="credential">The credential to be used when interacting with Openstack.</param>
        /// <param name="cancellationToken">The cancellation token to be used when interacting with Openstack.</param>
        public IdentityServicePocoClient(IOpenstackCredential credential, CancellationToken cancellationToken)
        {
            credential.AssertIsNotNull("credential");
            cancellationToken.AssertIsNotNull("cancellationToken");

            this.credential = credential;
            this.cancellationToken = cancellationToken;
        }

        /// <inheritdoc/>
        public async Task<IOpenstackCredential> Authenticate()
        {
            var client = ServiceLocator.Instance.Locate<IIdentityServiceRestClientFactory>().Create(this.credential, this.cancellationToken);

            var resp = await client.Authenticate();

            if (resp.StatusCode != HttpStatusCode.OK && resp.StatusCode != HttpStatusCode.NonAuthoritativeInformation)
            {
                throw new InvalidOperationException(string.Format("Failed to authenticate. The remote server returned the following status code: '{0}'.", resp.StatusCode));
            }

            var payload = await resp.ReadContentAsStringAsync();

            var tokenConverter = ServiceLocator.Instance.Locate<IAccessTokenPayloadConverter>();
            var accessToken = tokenConverter.Convert(payload);

            var scConverter = ServiceLocator.Instance.Locate<IOpenstackServiceCatalogPayloadConverter>();
            var serviceCatalog = scConverter.Convert(payload);

            this.credential.SetAccessTokenId(accessToken);
            this.credential.SetServiceCatalog(serviceCatalog);

            if (string.IsNullOrEmpty(this.credential.Region))
            {
                var resolver = ServiceLocator.Instance.Locate<IOpenstackRegionResolver>();
                var region = resolver.Resolve(this.credential.AuthenticationEndpoint, this.credential.ServiceCatalog, IdentityServiceName);

                //TODO: figure out if we want to throw in the case where the region cannot be resolved... 

                this.credential.SetRegion(region);
            }

            return this.credential;
        }
    }
}
