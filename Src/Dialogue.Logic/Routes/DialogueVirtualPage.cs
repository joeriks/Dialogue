﻿using System;
using Dialogue.Logic.Application;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;

namespace Dialogue.Logic.Routes
{
    /// <summary>
    /// Used to create a fake dynamic umbraco page for rendering tag lists, tag pages and search results (any virtual route)
    /// </summary>
    internal class DialogueVirtualPage : PublishedContentWrapped
    {
        private readonly string _pageName;
        private readonly string _pageTypeAlias;
        private readonly string _urlPath;

        public DialogueVirtualPage(IPublishedContent rootBlogPage, string pageName, string pageTypeAlias, string urlPath = null)
            : base(rootBlogPage)
        {
            if (pageName == null) throw new ArgumentNullException("pageName");
            if (pageTypeAlias == null) throw new ArgumentNullException("pageTypeAlias");
            _pageName = pageName;
            _pageTypeAlias = pageTypeAlias;

            if (urlPath != null)
            {
                _urlPath = urlPath.SafeEncodeUrlSegments();
            }

        }

        public override string Url
        {
            get { return base.Url.EnsureEndsWith('/') + (_urlPath ?? UrlName); }
        }

        public override PublishedContentType ContentType
        {
            get { return null; }
        }

        public override IPublishedContent Parent
        {
            get { return Content; }
        }

        public override int Id
        {
            get { return int.MaxValue - Parent.Id; }
        }

        public override string Name
        {
            get { return _pageName; }
        }

        public override string UrlName
        {
            get { return _pageName.ToLowerInvariant(); }
        }

        public override string DocumentTypeAlias
        {
            get { return _pageTypeAlias; }
        }

        public override int DocumentTypeId
        {
            get { return int.MaxValue - Parent.DocumentTypeId; }
        }

        public override string Path
        {
            get { return Content.Path.EnsureEndsWith(',') + Id; }
        }

        public override int Level
        {
            get { return Content.Level + 1; }
        }
    }
}