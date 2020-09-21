﻿using System;
using System.Drawing;
using System.IO;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Configuration.Models;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.IO;
using Umbraco.Core.Models;

namespace Umbraco.Web.Media
{
    /// <summary>
    /// Provides methods to manage auto-fill properties for upload fields.
    /// </summary>
    public class UploadAutoFillProperties
    {
        private readonly IMediaFileSystem _mediaFileSystem;
        private readonly ILogger<UploadAutoFillProperties> _logger;
        private readonly ContentSettings _contentSettings;

        public UploadAutoFillProperties(
            IMediaFileSystem mediaFileSystem,
            ILogger<UploadAutoFillProperties> logger,
            IOptions<ContentSettings> contentSettings)
        {
            _mediaFileSystem = mediaFileSystem ?? throw new ArgumentNullException(nameof(mediaFileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _contentSettings = contentSettings.Value ?? throw new ArgumentNullException(nameof(contentSettings));
        }

        /// <summary>
        /// Resets the auto-fill properties of a content item, for a specified auto-fill configuration.
        /// </summary>
        /// <param name="content">The content item.</param>
        /// <param name="autoFillConfig">The auto-fill configuration.</param>
        /// <param name="culture">Variation language.</param>
        /// <param name="segment">Variation segment.</param>
        public void Reset(IContentBase content, IImagingAutoFillUploadField autoFillConfig, string culture, string segment)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (autoFillConfig == null) throw new ArgumentNullException(nameof(autoFillConfig));

            ResetProperties(content, autoFillConfig, culture, segment);
        }

        /// <summary>
        /// Populates the auto-fill properties of a content item, for a specified auto-fill configuration.
        /// </summary>
        /// <param name="content">The content item.</param>
        /// <param name="autoFillConfig">The auto-fill configuration.</param>
        /// <param name="filepath">The filesystem path to the uploaded file.</param>
        /// <remarks>The <paramref name="filepath"/> parameter is the path relative to the filesystem.</remarks>
        /// <param name="culture">Variation language.</param>
        /// <param name="segment">Variation segment.</param>
        public void Populate(IContentBase content, IImagingAutoFillUploadField autoFillConfig, string filepath, string culture, string segment)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (autoFillConfig == null) throw new ArgumentNullException(nameof(autoFillConfig));

            // no file = reset, file = auto-fill
            if (filepath.IsNullOrWhiteSpace())
            {
                ResetProperties(content, autoFillConfig, culture, segment);
            }
            else
            {
                // if anything goes wrong, just reset the properties
                try
                {
                    using (var filestream = _mediaFileSystem.OpenFile(filepath))
                    {
                        var extension = (Path.GetExtension(filepath) ?? "").TrimStart('.');
                        var size = _contentSettings.IsImageFile(extension) ? (Size?)ImageHelper.GetDimensions(filestream) : null;
                        SetProperties(content, autoFillConfig, size, filestream.Length, extension, culture, segment);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not populate upload auto-fill properties for file '{File}'.", filepath);
                    ResetProperties(content, autoFillConfig, culture, segment);
                }
            }
        }

        /// <summary>
        /// Populates the auto-fill properties of a content item.
        /// </summary>
        /// <param name="content">The content item.</param>
        /// <param name="autoFillConfig"></param>
        /// <param name="filepath">The filesystem-relative filepath, or null to clear properties.</param>
        /// <param name="filestream">The stream containing the file data.</param>
        /// <param name="culture">Variation language.</param>
        /// <param name="segment">Variation segment.</param>
        public void Populate(IContentBase content, IImagingAutoFillUploadField autoFillConfig, string filepath, Stream filestream, string culture, string segment)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (autoFillConfig == null) throw new ArgumentNullException(nameof(autoFillConfig));

            // no file = reset, file = auto-fill
            if (filepath.IsNullOrWhiteSpace() || filestream == null)
            {
                ResetProperties(content, autoFillConfig, culture, segment);
            }
            else
            {
                var extension = (Path.GetExtension(filepath) ?? "").TrimStart('.');
                var size = _contentSettings.IsImageFile(extension) ? (Size?)ImageHelper.GetDimensions(filestream) : null;
                SetProperties(content, autoFillConfig, size, filestream.Length, extension, culture, segment);
            }
        }

        private static void SetProperties(IContentBase content, IImagingAutoFillUploadField autoFillConfig, Size? size, long length, string extension, string culture, string segment)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (autoFillConfig == null) throw new ArgumentNullException(nameof(autoFillConfig));

            if (!string.IsNullOrWhiteSpace(autoFillConfig.WidthFieldAlias) && content.Properties.Contains(autoFillConfig.WidthFieldAlias))
                content.Properties[autoFillConfig.WidthFieldAlias].SetValue(size.HasValue ? size.Value.Width.ToInvariantString() : string.Empty, culture, segment);

            if (!string.IsNullOrWhiteSpace(autoFillConfig.HeightFieldAlias) && content.Properties.Contains(autoFillConfig.HeightFieldAlias))
                content.Properties[autoFillConfig.HeightFieldAlias].SetValue(size.HasValue ? size.Value.Height.ToInvariantString() : string.Empty, culture, segment);

            if (!string.IsNullOrWhiteSpace(autoFillConfig.LengthFieldAlias) && content.Properties.Contains(autoFillConfig.LengthFieldAlias))
                content.Properties[autoFillConfig.LengthFieldAlias].SetValue(length, culture, segment);

            if (!string.IsNullOrWhiteSpace(autoFillConfig.ExtensionFieldAlias) && content.Properties.Contains(autoFillConfig.ExtensionFieldAlias))
                content.Properties[autoFillConfig.ExtensionFieldAlias].SetValue(extension, culture, segment);
        }

        private static void ResetProperties(IContentBase content, IImagingAutoFillUploadField autoFillConfig, string culture, string segment)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (autoFillConfig == null) throw new ArgumentNullException(nameof(autoFillConfig));

            if (content.Properties.Contains(autoFillConfig.WidthFieldAlias))
                content.Properties[autoFillConfig.WidthFieldAlias].SetValue(string.Empty, culture, segment);

            if (content.Properties.Contains(autoFillConfig.HeightFieldAlias))
                content.Properties[autoFillConfig.HeightFieldAlias].SetValue(string.Empty, culture, segment);

            if (content.Properties.Contains(autoFillConfig.LengthFieldAlias))
                content.Properties[autoFillConfig.LengthFieldAlias].SetValue(string.Empty, culture, segment);

            if (content.Properties.Contains(autoFillConfig.ExtensionFieldAlias))
                content.Properties[autoFillConfig.ExtensionFieldAlias].SetValue(string.Empty, culture, segment);
        }
    }
}