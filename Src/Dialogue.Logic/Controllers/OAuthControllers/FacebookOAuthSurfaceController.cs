﻿using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web.Hosting;
using System.Web.Mvc;
using System.Web.Security;
using Dialogue.Logic.Application;
using Dialogue.Logic.Constants;
using Dialogue.Logic.Models;
using Dialogue.Logic.Models.OAuth;
using Dialogue.Logic.Models.ViewModels;
using Dialogue.Logic.Services;
using Skybrud.Social.Facebook;
using Skybrud.Social.Facebook.OAuth;
using Skybrud.Social.Json;

namespace Dialogue.Logic.Controllers.OAuthControllers
{
    public class FacebookOAuthSurfaceController : BaseSurfaceController
    {
        public string ReturnUrl
        {
            get { return string.Concat(AppHelpers.ReturnCurrentDomain(), Urls.GenerateUrl(Urls.UrlType.FacebookLogin)); }
        }

        public string Callback { get; private set; }

        public string ContentTypeAlias { get; private set; }

        public string PropertyAlias { get; private set; }

        /// <summary>
        /// Gets the authorizing code from the query string (if specified).
        /// </summary>
        public string AuthCode
        {
            get { return Request.QueryString["code"]; }
        }

        public string AuthState
        {
            get { return Request.QueryString["state"]; }
        }

        public string AuthErrorReason
        {
            get { return Request.QueryString["error_reason"]; }
        }

        public string AuthError
        {
            get { return Request.QueryString["error"]; }
        }

        public string AuthErrorDescription
        {
            get { return Request.QueryString["error_description"]; }
        }

        public ActionResult FacebookLogin()
        {
            var resultMessage = new GenericMessageViewModel();

            Callback = Request.QueryString["callback"];
            ContentTypeAlias = Request.QueryString["contentTypeAlias"];
            PropertyAlias = Request.QueryString["propertyAlias"];

            if (AuthState != null)
            {
                var stateValue = Session["Dialogue_" + AuthState] as string[];
                if (stateValue != null && stateValue.Length == 3)
                {
                    Callback = stateValue[0];
                    ContentTypeAlias = stateValue[1];
                    PropertyAlias = stateValue[2];
                }
            }

            // Get the prevalue options
            if (string.IsNullOrEmpty(Dialogue.Settings().FacebookAppId) || string.IsNullOrEmpty(Dialogue.Settings().FacebookAppSecret))
            {
                resultMessage.Message = "You need to add the Facebook app credentials";
                resultMessage.MessageType = GenericMessages.Danger;
            }
            else
            {

                // Settings valid move on
                // Configure the OAuth client based on the options of the prevalue options
                var client = new FacebookOAuthClient
                {
                    AppId = Dialogue.Settings().FacebookAppId,
                    AppSecret = Dialogue.Settings().FacebookAppSecret,
                    RedirectUri = ReturnUrl
                };

                // Session expired?
                if (AuthState != null && Session["Dialogue_" + AuthState] == null)
                {
                    resultMessage.Message = "Session Expired";
                    resultMessage.MessageType = GenericMessages.Danger;
                }

                // Check whether an error response was received from Facebook
                if (AuthError != null)
                {
                    resultMessage.Message = AuthErrorDescription;
                    resultMessage.MessageType = GenericMessages.Danger;
                }

                // Redirect the user to the Facebook login dialog
                if (AuthCode == null)
                {
                    // Generate a new unique/random state
                    var state = Guid.NewGuid().ToString();

                    // Save the state in the current user session
                    Session["Dialogue_" + state] = new[] { Callback, ContentTypeAlias, PropertyAlias };

                    // Construct the authorization URL
                    var url = client.GetAuthorizationUrl(state, "public_profile", "email"); //"user_friends"

                    // Redirect the user
                    return Redirect(url);
                }

                // Exchange the authorization code for a user access token
                var userAccessToken = string.Empty;
                try
                {
                    userAccessToken = client.GetAccessTokenFromAuthCode(AuthCode);
                }
                catch (Exception ex)
                {
                    resultMessage.Message = string.Format("Unable to acquire access token<br/>{0}", ex.Message);
                    resultMessage.MessageType = GenericMessages.Danger;
                }

                try
                {
                    if (string.IsNullOrEmpty(resultMessage.Message))
                    {
                        // Initialize the Facebook service (no calls are made here)
                        var service = FacebookService.CreateFromAccessToken(userAccessToken);

                        // Hack to get email
                        // Get the raw string and parse it
                        // we use this to get items manually including the email
                        var response = service.Methods.Raw.Me();
                        var obj = JsonObject.ParseJson(response);

                        // Make a call to the Facebook API to get information about the user
                        var me = service.Methods.Me();

                        
                        // Set the callback data
                        var data = new FacebookOAuthData
                        {
                            Id = me.Id,
                            Name = me.Name ?? me.UserName,
                            AccessToken = userAccessToken,                            
                            Email = obj.GetString("email")
                        };

                        // First see if this user has registered already - Use email address
                        using (UnitOfWorkManager.NewUnitOfWork())
                        {
                            var userExists = AppHelpers.UmbServices().MemberService.GetByEmail(data.Email);

                            if (userExists != null)
                            {
                                // Update access token
                                userExists.Properties[AppConstants.PropMemberFacebookAccessToken].Value = userAccessToken;
                                AppHelpers.UmbServices().MemberService.Save(userExists);

                                // Users already exists, so log them in
                                FormsAuthentication.SetAuthCookie(userExists.Username, true);
                                resultMessage.Message = Lang("Members.NowLoggedIn");
                                resultMessage.MessageType = GenericMessages.Success;
                            }
                            else
                            {
                                // Not registered already so register them
                                var viewModel = new RegisterViewModel
                                {
                                    Email = data.Email,
                                    LoginType = LoginType.Facebook,
                                    Password = AppHelpers.RandomString(8),
                                    UserName = data.Name,
                                    UserAccessToken = userAccessToken
                                };

                                // Get the image and save it
                                var getImageUrl = string.Format("http://graph.facebook.com/{0}/picture?type=normal", me.Id);
                                viewModel.SocialProfileImageUrl = getImageUrl;

                                return RedirectToAction("MemberRegisterLogic", "DialogueLoginRegisterSurface", viewModel);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    resultMessage.Message = string.Format("Unable to get user information<br/>{0}", ex.Message);
                    resultMessage.MessageType = GenericMessages.Danger;
                }

            }

            ShowMessage(resultMessage);
            return RedirectToUmbracoPage(Dialogue.Settings().ForumId);
        }
    } 
}