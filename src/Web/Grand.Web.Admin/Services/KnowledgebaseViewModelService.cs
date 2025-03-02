﻿using Grand.Business.Common.Extensions;
using Grand.Business.Common.Interfaces.Directory;
using Grand.Business.Common.Interfaces.Localization;
using Grand.Business.Common.Interfaces.Logging;
using Grand.Business.Common.Interfaces.Seo;
using Grand.Business.Customers.Interfaces;
using Grand.Business.Marketing.Extensions;
using Grand.Business.Marketing.Interfaces.Knowledgebase;
using Grand.Domain.Knowledgebase;
using Grand.Domain.Seo;
using Grand.Infrastructure;
using Grand.Web.Admin.Extensions;
using Grand.Web.Admin.Interfaces;
using Grand.Web.Admin.Models.Knowledgebase;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grand.Web.Admin.Services
{
    public partial class KnowledgebaseViewModelService : IKnowledgebaseViewModelService
    {
        private readonly ITranslationService _translationService;
        private readonly IKnowledgebaseService _knowledgebaseService;
        private readonly ICustomerActivityService _customerActivityService;
        private readonly ICustomerService _customerService;
        private readonly IDateTimeService _dateTimeService;
        private readonly ISlugService _slugService;
        private readonly ILanguageService _languageService;
        private readonly IWorkContext _workContext;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private readonly SeoSettings _seoSettings;

        public KnowledgebaseViewModelService(ITranslationService translationService,
            IKnowledgebaseService knowledgebaseService, 
            ICustomerActivityService customerActivityService,
            ICustomerService customerService, 
            IDateTimeService dateTimeService, 
            ISlugService slugService,
            ILanguageService languageService,
            IWorkContext workContext,
            IHttpContextAccessor httpContextAccessor,
            SeoSettings seoSettings)
        {
            _translationService = translationService;
            _knowledgebaseService = knowledgebaseService;
            _customerActivityService = customerActivityService;
            _customerService = customerService;
            _dateTimeService = dateTimeService;
            _slugService = slugService;
            _languageService = languageService;
            _workContext = workContext;
            _httpContextAccessor = httpContextAccessor;
            _seoSettings = seoSettings;
        }

        protected virtual void FillChildNodes(TreeNode parentNode, List<ITreeNode> nodes)
        {
            var children = nodes.Where(x => x.ParentCategoryId == parentNode.id);
            foreach (var child in children)
            {
                var newNode = new TreeNode
                {
                    id = child.Id,
                    text = child.Name,
                    isCategory = child.GetType() == typeof(KnowledgebaseCategory),
                    nodes = new List<TreeNode>()
                };

                FillChildNodes(newNode, nodes);

                parentNode.nodes.Add(newNode);
            }
        }
        public virtual async Task PrepareCategory(KnowledgebaseCategoryModel model)
        {
            model.Categories.Add(new SelectListItem { Text = "[None]", Value = "" });
            var categories = await _knowledgebaseService.GetKnowledgebaseCategories();
            foreach (var category in categories)
            {
                model.Categories.Add(new SelectListItem
                {
                    Value = category.Id,
                    Text = category.GetFormattedBreadCrumb(categories)
                });
            }
        }
        public virtual async Task PrepareCategory(KnowledgebaseArticleModel model)
        {
            model.Categories.Add(new SelectListItem { Text = "[None]", Value = "" });
            var categories = await _knowledgebaseService.GetKnowledgebaseCategories();
            foreach (var category in categories)
            {
                model.Categories.Add(new SelectListItem
                {
                    Value = category.Id,
                    Text = category.GetFormattedBreadCrumb(categories)
                });
            }
        }
        public virtual async Task<List<TreeNode>> PrepareTreeNode()
        {
            var categories = await _knowledgebaseService.GetKnowledgebaseCategories();
            var articles = await _knowledgebaseService.GetKnowledgebaseArticles();
            List<TreeNode> nodeList = new List<TreeNode>();

            List<ITreeNode> list = new List<ITreeNode>();
            list.AddRange(categories);
            list.AddRange(articles);

            foreach (var node in list)
            {
                if (string.IsNullOrEmpty(node.ParentCategoryId))
                {
                    var newNode = new TreeNode
                    {
                        id = node.Id,
                        text = node.Name,
                        isCategory = node.GetType() == typeof(KnowledgebaseCategory),
                        nodes = new List<TreeNode>()
                    };

                    FillChildNodes(newNode, list);

                    nodeList.Add(newNode);
                }
            }
            return nodeList;
        }
        public virtual async Task<(IEnumerable<KnowledgebaseArticleGridModel> knowledgebaseArticleGridModels, int totalCount)> PrepareKnowledgebaseArticleGridModel(string parentCategoryId, int pageIndex, int pageSize)
        {
            var articles = await _knowledgebaseService.GetKnowledgebaseArticlesByCategoryId(parentCategoryId, pageIndex - 1, pageSize);
            return (articles.Select(x => new KnowledgebaseArticleGridModel
            {
                Name = x.Name,
                DisplayOrder = x.DisplayOrder,
                Published = x.Published,
                ArticleId = x.Id,
                Id = x.Id
            }), articles.TotalCount);
        }
        public virtual async Task<(IEnumerable<KnowledgebaseCategoryModel.ActivityLogModel> activityLogModels, int totalCount)> PrepareCategoryActivityLogModels(string categoryId, int pageIndex, int pageSize)
        {
            var activityLog = await _customerActivityService.GetKnowledgebaseCategoryActivities(null, null, categoryId, pageIndex - 1, pageSize);
            var items = new List<KnowledgebaseCategoryModel.ActivityLogModel>();
            foreach (var x in activityLog)
            {
                var customer = await _customerService.GetCustomerById(x.CustomerId);
                var m = new KnowledgebaseCategoryModel.ActivityLogModel
                {
                    Id = x.Id,
                    ActivityLogTypeName = (await _customerActivityService.GetActivityTypeById(x.ActivityLogTypeId))?.Name,
                    Comment = x.Comment,
                    CreatedOn = _dateTimeService.ConvertToUserTime(x.CreatedOnUtc, DateTimeKind.Utc),
                    CustomerId = x.CustomerId,
                    CustomerEmail = customer != null ? customer.Email : "null"
                };
                items.Add(m);
            }
            return (items, activityLog.TotalCount);
        }
        public virtual async Task<(IEnumerable<KnowledgebaseArticleModel.ActivityLogModel> activityLogModels, int totalCount)> PrepareArticleActivityLogModels(string articleId, int pageIndex, int pageSize)
        {
            var activityLog = await _customerActivityService.GetKnowledgebaseArticleActivities(null, null, articleId, pageIndex - 1, pageSize);
            var items = new List<KnowledgebaseArticleModel.ActivityLogModel>();
            foreach (var x in activityLog)
            {
                var customer = await _customerService.GetCustomerById(x.CustomerId);
                var m = new KnowledgebaseArticleModel.ActivityLogModel
                {
                    Id = x.Id,
                    ActivityLogTypeName = (await _customerActivityService.GetActivityTypeById(x.ActivityLogTypeId))?.Name,
                    Comment = x.Comment,
                    CreatedOn = _dateTimeService.ConvertToUserTime(x.CreatedOnUtc, DateTimeKind.Utc),
                    CustomerId = x.CustomerId,
                    CustomerEmail = customer != null ? customer.Email : "null"
                };
                items.Add(m);
            }
            return (items, activityLog.TotalCount);
        }
        public virtual async Task<KnowledgebaseCategoryModel> PrepareKnowledgebaseCategoryModel()
        {
            var model = new KnowledgebaseCategoryModel();
            model.Published = true;
            await PrepareCategory(model);
            return model;
        }

        public virtual async Task<KnowledgebaseCategory> InsertKnowledgebaseCategoryModel(KnowledgebaseCategoryModel model)
        {
            var knowledgebaseCategory = model.ToEntity();
            knowledgebaseCategory.CreatedOnUtc = DateTime.UtcNow;
            knowledgebaseCategory.UpdatedOnUtc = DateTime.UtcNow;
            knowledgebaseCategory.Locales = await model.Locales.ToTranslationProperty(knowledgebaseCategory, x => x.Name, _seoSettings, _slugService, _languageService);
            model.SeName = await knowledgebaseCategory.ValidateSeName(model.SeName, knowledgebaseCategory.Name, true, _seoSettings, _slugService, _languageService);
            knowledgebaseCategory.SeName = model.SeName;
            await _knowledgebaseService.InsertKnowledgebaseCategory(knowledgebaseCategory);
            await _slugService.SaveSlug(knowledgebaseCategory, model.SeName, "");
            _ = _customerActivityService.InsertActivity("CreateKnowledgebaseCategory", knowledgebaseCategory.Id,
                _workContext.CurrentCustomer, _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                _translationService.GetResource("ActivityLog.CreateKnowledgebaseCategory"), knowledgebaseCategory.Name);

            return knowledgebaseCategory;
        }
        public virtual async Task<KnowledgebaseCategory> UpdateKnowledgebaseCategoryModel(KnowledgebaseCategory knowledgebaseCategory, KnowledgebaseCategoryModel model)
        {
            knowledgebaseCategory = model.ToEntity(knowledgebaseCategory);
            knowledgebaseCategory.UpdatedOnUtc = DateTime.UtcNow;
            knowledgebaseCategory.Locales = await model.Locales.ToTranslationProperty(knowledgebaseCategory, x => x.Name, _seoSettings, _slugService, _languageService);
            model.SeName = await knowledgebaseCategory.ValidateSeName(model.SeName, knowledgebaseCategory.Name, true, _seoSettings, _slugService, _languageService);
            knowledgebaseCategory.SeName = model.SeName;
            await _knowledgebaseService.UpdateKnowledgebaseCategory(knowledgebaseCategory);
            await _slugService.SaveSlug(knowledgebaseCategory, model.SeName, "");
            _ = _customerActivityService.InsertActivity("UpdateKnowledgebaseCategory", knowledgebaseCategory.Id,
                _workContext.CurrentCustomer, _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                _translationService.GetResource("ActivityLog.UpdateKnowledgebaseCategory"), knowledgebaseCategory.Name);

            return knowledgebaseCategory;
        }
        public virtual async Task DeleteKnowledgebaseCategoryModel(KnowledgebaseCategory knowledgebaseCategory)
        {
            await _knowledgebaseService.DeleteKnowledgebaseCategory(knowledgebaseCategory);
            _ = _customerActivityService.InsertActivity("DeleteKnowledgebaseCategory", knowledgebaseCategory.Id,
                _workContext.CurrentCustomer, _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                _translationService.GetResource("ActivityLog.DeleteKnowledgebaseCategory"), knowledgebaseCategory.Name);
        }
        public virtual async Task<KnowledgebaseArticleModel> PrepareKnowledgebaseArticleModel()
        {
            var model = new KnowledgebaseArticleModel
            {
                Published = true,
                AllowComments = true
            };
            await PrepareCategory(model);
            return model;
        }
        public virtual async Task<KnowledgebaseArticle> InsertKnowledgebaseArticleModel(KnowledgebaseArticleModel model)
        {
            var knowledgebaseArticle = model.ToEntity();
            knowledgebaseArticle.CreatedOnUtc = DateTime.UtcNow;
            knowledgebaseArticle.UpdatedOnUtc = DateTime.UtcNow;
            knowledgebaseArticle.Locales = await model.Locales.ToTranslationProperty(knowledgebaseArticle, x => x.Name, _seoSettings, _slugService, _languageService);
            model.SeName = await knowledgebaseArticle.ValidateSeName(model.SeName, knowledgebaseArticle.Name, true, _seoSettings, _slugService, _languageService);
            knowledgebaseArticle.SeName = model.SeName;
            knowledgebaseArticle.AllowComments = model.AllowComments;
            await _knowledgebaseService.InsertKnowledgebaseArticle(knowledgebaseArticle);
            await _slugService.SaveSlug(knowledgebaseArticle, model.SeName, "");
            _ = _customerActivityService.InsertActivity("CreateKnowledgebaseArticle", knowledgebaseArticle.Id,
                _workContext.CurrentCustomer, _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                _translationService.GetResource("ActivityLog.CreateKnowledgebaseArticle"), knowledgebaseArticle.Name);

            return knowledgebaseArticle;
        }
        public virtual async Task<KnowledgebaseArticle> UpdateKnowledgebaseArticleModel(KnowledgebaseArticle knowledgebaseArticle, KnowledgebaseArticleModel model)
        {
            knowledgebaseArticle = model.ToEntity(knowledgebaseArticle);
            knowledgebaseArticle.UpdatedOnUtc = DateTime.UtcNow;
            knowledgebaseArticle.Locales = await model.Locales.ToTranslationProperty(knowledgebaseArticle, x => x.Name, _seoSettings, _slugService, _languageService);
            model.SeName = await knowledgebaseArticle.ValidateSeName(model.SeName, knowledgebaseArticle.Name, true, _seoSettings, _slugService, _languageService);
            knowledgebaseArticle.SeName = model.SeName;
            knowledgebaseArticle.AllowComments = model.AllowComments;
            await _knowledgebaseService.UpdateKnowledgebaseArticle(knowledgebaseArticle);
            await _slugService.SaveSlug(knowledgebaseArticle, model.SeName, "");
            _ = _customerActivityService.InsertActivity("UpdateKnowledgebaseArticle", knowledgebaseArticle.Id,
                _workContext.CurrentCustomer, _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                _translationService.GetResource("ActivityLog.UpdateKnowledgebaseArticle"), knowledgebaseArticle.Name);

            return knowledgebaseArticle;
        }
        public virtual async Task DeleteKnowledgebaseArticle(KnowledgebaseArticle knowledgebaseArticle)
        {
            await _knowledgebaseService.DeleteKnowledgebaseArticle(knowledgebaseArticle);
            _ = _customerActivityService.InsertActivity("DeleteKnowledgebaseArticle", knowledgebaseArticle.Id,
                _workContext.CurrentCustomer, _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                _translationService.GetResource("ActivityLog.DeleteKnowledgebaseArticle"), knowledgebaseArticle.Name);
        }
        public virtual async Task InsertKnowledgebaseRelatedArticle(KnowledgebaseArticleModel.AddRelatedArticleModel model)
        {
            var article = await _knowledgebaseService.GetKnowledgebaseArticle(model.ArticleId);

            foreach (var id in model.SelectedArticlesIds)
            {
                if (id != article.Id)
                    if (!article.RelatedArticles.Contains(id))
                        article.RelatedArticles.Add(id);
            }
            await _knowledgebaseService.UpdateKnowledgebaseArticle(article);
        }
        public virtual async Task DeleteKnowledgebaseRelatedArticle(KnowledgebaseArticleModel.AddRelatedArticleModel model)
        {
            var article = await _knowledgebaseService.GetKnowledgebaseArticle(model.ArticleId);
            var related = await _knowledgebaseService.GetKnowledgebaseArticle(model.Id);

            if (article == null || related == null)
                throw new ArgumentNullException("No article found with specified id");

            string toDelete = "";
            foreach (var item in article.RelatedArticles)
            {
                if (item == related.Id)
                    toDelete = item;
            }

            if (!string.IsNullOrEmpty(toDelete))
                article.RelatedArticles.Remove(toDelete);

            await _knowledgebaseService.UpdateKnowledgebaseArticle(article);
        }
    }
}
