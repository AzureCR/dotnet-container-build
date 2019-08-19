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
    /// Returns the requested OCI Manifest file
    /// </summary>
    [Newtonsoft.Json.JsonObject("application/vnd.oci.image.manifest.v1+json")]
    public partial class OCIManifest : Manifest
    {
        /// <summary>
        /// Initializes a new instance of the OCIManifest class.
        /// </summary>
        public OCIManifest()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the OCIManifest class.
        /// </summary>
        /// <param name="schemaVersion">Schema version</param>
        /// <param name="config">V2 image config descriptor</param>
        /// <param name="layers">List of V2 image layer information</param>
        public OCIManifest(int? schemaVersion = default(int?), V2Descriptor config = default(V2Descriptor), IList<V2Descriptor> layers = default(IList<V2Descriptor>))
            : base(schemaVersion)
        {
            Config = config;
            Layers = layers;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// Gets or sets V2 image config descriptor
        /// </summary>
        [JsonProperty(PropertyName = "config")]
        public V2Descriptor Config { get; set; }

        /// <summary>
        /// Gets or sets list of V2 image layer information
        /// </summary>
        [JsonProperty(PropertyName = "layers")]
        public IList<V2Descriptor> Layers { get; set; }

    }
}
