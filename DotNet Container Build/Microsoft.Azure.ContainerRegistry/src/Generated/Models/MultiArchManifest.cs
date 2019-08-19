// <auto-generated>
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for
// license information.
//
// Code generated by Microsoft (R) AutoRest Code Generator.
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>

namespace Microsoft.Azure.ContainerRegistry.Models
{
    using Newtonsoft.Json;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Returns the requested Docker multi-arch-manifest file
    /// </summary>
    [Newtonsoft.Json.JsonObject("application/vnd.docker.distribution.manifest.list.v2+json")]
    public partial class MultiArchManifest : Manifest
    {
        /// <summary>
        /// Initializes a new instance of the MultiArchManifest class.
        /// </summary>
        public MultiArchManifest()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the MultiArchManifest class.
        /// </summary>
        /// <param name="schemaVersion">Schema version</param>
        /// <param name="manifests">List of V2 image layer information</param>
        public MultiArchManifest(int? schemaVersion = default(int?), IList<MultiArchManifestAttributes> manifests = default(IList<MultiArchManifestAttributes>))
            : base(schemaVersion)
        {
            Manifests = manifests;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// Gets or sets list of V2 image layer information
        /// </summary>
        [JsonProperty(PropertyName = "manifests")]
        public IList<MultiArchManifestAttributes> Manifests { get; set; }

    }
}
