using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System;

namespace Twitchmata {
    public class TwitchmataAuthentication : EditorWindow {

        [MenuItem("Window/Twitchmata/Authenticate"), MenuItem("CONTEXT/TwitchManager/Twitchmata/Authenticate")]
        public static void ShowWindow() {
            var window = GetWindow<TwitchmataAuthentication>();
            window.titleContent = new GUIContent("Authenticate Twitchmata");
        }

        private TwitchManager TwitchManager;

        public VisualTreeAsset WindowXML;
        public void CreateGUI() {
            var selectedObject = Selection.activeGameObject;
            if (selectedObject == null) {
                this.DisplayInfoLabel("You must select a GameObject containing a TwitchManager before opening this window");
                return;
            }
            this.TwitchManager = selectedObject.GetComponent<TwitchManager>();
            if (this.TwitchManager == null) {
                this.DisplayInfoLabel("You must select a GameObject containing a TwitchManager before opening this window");
                return;
            }

            if (this.EnsureWindowXMLExists() == false) {
                this.DisplayInfoLabel("Could not find Auth UI. \n\nEnsure Twitchmata is in Assets/External/Twitchmata. Alternatively, select Editor/TwitchmataAuthentication.cs in Unity's Project panel and drag the UXML file to the Window XML field of the inspector");
                return;
            }

            var ui = this.WindowXML.CloneTree();
            this.rootVisualElement.Add(ui);

            var authChannelButton = ui.Query<Button>("authenticate_channel").First();
            authChannelButton.RegisterCallback<MouseUpEvent>(OpenAuthenticateChannel);

            var authBotButton = ui.Query<Button>("authenticate_chat_bot").First();
            authBotButton.RegisterCallback<MouseUpEvent>(OpenAuthenticateBot);

            var saveChannelAuthButton = ui.Query<Button>("save_channel_auth").First();
            saveChannelAuthButton.RegisterCallback<MouseUpEvent>(SaveChannelAuth);

            var saveBotAuthButton = ui.Query<Button>("save_bot_auth").First();
            saveBotAuthButton.RegisterCallback<MouseUpEvent>(SaveBotAuth);

            this.ChannelAuthField = ui.Query<TextField>("channel_auth_field").First();
            this.ChannelSuccessLabel = ui.Query<Label>("channel_success_label").First();
            this.ChannelErrorLabel = ui.Query<Label>("channel_error_label").First();
            this.BotAuthField = ui.Query<TextField>("bot_auth_field").First();
            this.BotSuccessLabel = ui.Query<Label>("bot_success_label").First();
            this.BotErrorLabel = ui.Query<Label>("bot_error_label").First();
        }

        private void DisplayInfoLabel(string labelText) {
            var label = new Label(labelText);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 20;
            this.rootVisualElement.Add(label);
        }

        private bool EnsureWindowXMLExists() {
            if (this.WindowXML != null) {
                return true;
            }
            this.WindowXML = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/External/Twitchmata/Editor/TwitchmataAutomation_UXML.uxml");
            return this.WindowXML != null;
        }

        private TextField ChannelAuthField;
        private Label ChannelSuccessLabel;
        private Label ChannelErrorLabel;
        private TextField BotAuthField;
        private Label BotSuccessLabel;
        private Label BotErrorLabel;


        string[] scopes = {
            "moderator:read:chat_settings",
            "moderator:read:followers",
            "moderator:read:blocked_terms",
            "moderator:read:unban_requests",
            "moderator:read:banned_users",
            "moderator:read:chat_messages",
            "moderator:read:chatters",
            "moderator:read:warnings",
            "moderator:read:moderators",
            "moderator:read:vips",
            "moderator:manage:shoutouts",
            "moderator:manage:announcements",
            "moderator:manage:chat_settings",
            "bits:read",
            "user:read:follows",
            "user:read:chat",
            "user:manage:whispers",
            "channel:read:subscriptions",
            "channel:read:vips",
            "channel:read:hype_train",
            "channel:manage:raids",
            "channel:moderate",
            "channel:manage:redemptions",
            "channel:read:redemptions",
            "moderation:read",
            "chat:edit",
            "chat:read",
        };

        private string ChannelAuthState;
        private void OpenAuthenticateChannel(MouseUpEvent evt) {
            string clientID = this.TwitchManager.ConnectionConfig.ClientID;
            this.ChannelAuthState = Guid.NewGuid().ToString();
            var loginURL = "https://id.twitch.tv/oauth2/authorize?response_type=token&client_id="+ clientID + "&redirect_uri=http://localhost:3000&scope=" + string.Join('+', scopes).Replace(":", "%3A") + "&force_verify=true&state=" + this.ChannelAuthState;
            Application.OpenURL(loginURL);
        }

        private string BotAuthState;
        private void OpenAuthenticateBot(MouseUpEvent evt) {
            string clientID = this.TwitchManager.ConnectionConfig.ClientID;
            this.BotAuthState = Guid.NewGuid().ToString();
            var loginURL = "https://id.twitch.tv/oauth2/authorize?response_type=token&client_id=" + clientID + "&redirect_uri=http://localhost:3000&scope=" + string.Join('+', scopes).Replace(":", "%3A") + "&force_verify=true&state=" + this.BotAuthState;
            Application.OpenURL(loginURL);
        }

        private void SaveChannelAuth(MouseUpEvent evt) {
            try { 
                var channelURI = new Uri(this.ChannelAuthField.value);
                var channelToken = this.ExtractTokenFromFragment(channelURI.Fragment, this.ChannelAuthState);

                var persistencePath = this.TwitchManager.PersistencePath;
                if (persistencePath == null || persistencePath.Length == 0) {
                    persistencePath = Application.persistentDataPath;
                }
                var persistence = new Persistence(persistencePath);
                persistence.AccountAccessToken = channelToken;

                this.ChannelErrorLabel.style.display = DisplayStyle.None;
                this.ChannelSuccessLabel.style.display = DisplayStyle.Flex;
            } catch {
                this.ChannelErrorLabel.style.display = DisplayStyle.Flex;
                this.ChannelSuccessLabel.style.display = DisplayStyle.None;
                this.ChannelErrorLabel.text = "Could not complete authentication. Please double-check the pasted URL";
            }
        }

        private void SaveBotAuth(MouseUpEvent evt) {
            try {
                var botURI = new Uri(this.BotAuthField.value);
                var botToken = this.ExtractTokenFromFragment(botURI.Fragment, this.BotAuthState);

                var persistencePath = this.TwitchManager.PersistencePath;
                if (persistencePath == null || persistencePath.Length == 0)
                {
                    persistencePath = Application.persistentDataPath;
                }
                var persistence = new Persistence(persistencePath);
                persistence.BotAccessToken = botToken;

                this.BotErrorLabel.style.display = DisplayStyle.None;
                this.BotSuccessLabel.style.display = DisplayStyle.Flex;
            } catch {
                this.BotErrorLabel.style.display = DisplayStyle.Flex;
                this.BotSuccessLabel.style.display = DisplayStyle.None;
                this.BotErrorLabel.text = "Could not complete authentication. Please double-check the pasted URL";
            }
        }

        private string ExtractTokenFromFragment(string fragment, string expectedState) {
            string token = null;
            string state = null;
            var components = fragment.Substring(1).Split('&');
            foreach (var component in components) {
                var splitComponent = component.Split('=');
                if (splitComponent.Length != 2) {
                    continue;
                }
                if (splitComponent[0] == "access_token") {
                    token = splitComponent[1];
                }
                if (splitComponent[0] == "state") {
                    state = splitComponent[1];
                }
            }

            if (state != null && state != expectedState) {
                return null;
            }
            return token;
        }
    }
}
