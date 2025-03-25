using UnityEngine;
using System;
using System.Threading.Tasks;
using TwitchLib.Unity;

namespace Twitchmata {

    /// <summary>
    /// Root for Twitchmata.
    /// </summary>
    /// <remarks>
    /// Add this to a GameObject and then add any desired FeatureManagers to child GameObjects or the same GameObject.
    /// </remarks>
    public class TwitchManager : MonoBehaviour {

        #region Connection Management
        /// <summary>
        /// The current connection manager.
        /// </summary>
        /// <remarks>
        /// Usually you don't need to use this, but it's useful for accessing any Twitch functionality not handled by Twitchmata
        /// </remarks>
        public ConnectionManager ConnectionManager;

        public UserManager UserManager {
            get {
                return this.ConnectionManager.UserManager;
            }
        }



        /// <summary>
        /// Resets Twitchmata, setting up FeatureManagers again and connecting to Twitch.
        /// </summary>
        /// <remarks>
        /// This is a useful method to hook up to a debug button to reset during stream if there are any connection issuers
        /// </remarks>
        public void Reset() {
            Logger.LogInfo("Resetting connection");
            if (this.ConnectionManager != null) {
                this.ConnectionManager.Disconnect();
            }

            this.ConnectionManager = new ConnectionManager(this.ConnectionConfig, new Persistence(this.PersistencePath));
            this.ConnectionManager.PerformSetup(() => {
                this.DiscoverFeatureManagers();
                this.ConnectionManager.Connect();
                this.Utilities.ConnectionManager = this.ConnectionManager;
                this.DebugCommands = /*this.EnableDebugCommands ?*/ new DebugCommands(this) /*: null*/;
            });
        }
        #endregion

        #region Config
        /// <summary>
        /// The current configuration for connecting to Twitch.
        /// </summary>
        [Tooltip("The current configuration for connecting to Twitch")]
        public ConnectionConfig ConnectionConfig = new ConnectionConfig() {
            ClientID = "",
            ChannelName = "",
            BotName = "",
            PostConnectMessage = false,
            ConnectMessage =  "Twichmata connected!"
        };

        /// <summary>
        /// The location on disk where files are stored.
        /// </summary>
        /// 
        /// <remarks>
        /// Note: any files are stored in *plain text*. This is not secure for auth tokens but Unity does not provide a reliable way to store such secrets.
        /// In practice it's only an issue if a malicious actor gets onto your system and finds the authentication, but this is why you
        /// should keep the scopes you use in your authentication token to the minimum required.
        /// </remarks>
        [Tooltip("Location of files on disk. Leave blank to use default.")]
        public string PersistencePath;

        public LogLevel LogLevel = LogLevel.Error;
        #endregion

        #region Feature Managers

        public T GetFeatureManager<T>() where T:FeatureManager, new() {
            foreach (var featureManager in this.ConnectionManager.FeatureManagers) {
                var typedFeatureManager = featureManager as T;
                if (typedFeatureManager != null) {
                    return typedFeatureManager;
                }
            }
            
            var newFeatureManager = this.gameObject.AddComponent(typeof(T)) as T;
            newFeatureManager.Manager = this;
            this.ConnectionManager.RegisterFeatureManager(newFeatureManager);
            return newFeatureManager;
        }

        #endregion
     
        private DebugCommands DebugCommands = null;

        #region Utilities

        public Utilities Utilities = new Utilities();

        #endregion


        /**************************************************
         * INTERNAL CODE. NO NEED TO READ BELOW THIS LINE *
         **************************************************/

        #region Connection Management (private)
        TwitchManager() {
            Logger.TwitchManager = this;
        }

        private bool HasStarted = false;
        private void Start() {
            ThreadDispatcher.EnsureCreated("InvokeInternal");
            this.HasStarted = true;
            if (this.PersistencePath == null || this.PersistencePath == "") {
                this.PersistencePath = Application.persistentDataPath;
            }
            this.Reset();
        }

        private void DiscoverFeatureManagers() {
            foreach (var manager in this.GetComponentsInChildren<FeatureManager>()) {
                manager.Manager = this;
                this.ConnectionManager.RegisterFeatureManager(manager);
            }

            if (this.ConnectionManager.FeatureManagers.Count == 0) {
                Logger.LogWarning("No feature managers found");
            }

            this.ConnectionManager.PerformPostDiscoverySetup();
        }
        #endregion




        #region Threading Helpers
        internal static void RunTask<T>(Task<T> func, Action<T> action, Action<Exception> errorAction = null) {
            func.ContinueWith(delegate (Task<T> x) {
                try {
                    T value = x.Result;

                    ThreadDispatcher.Enqueue(delegate {
                        action(value);
                    });
                } catch (Exception e) {
                    if (errorAction != null) {
                        errorAction(e);
                        return;
                    }
                    Logger.LogError("Error getting result: " + e.Message);
                }
            });
        }
         
        internal static void RunTask(Task func, Action action, Action<Exception> errorAction = null) {
            ThreadDispatcher.EnsureCreated("InvokeInternal");
            func.ContinueWith(delegate (Task x) {
                try {
                    x.Wait();

                    ThreadDispatcher.Enqueue(delegate {
                        action();
                    });
                } catch (Exception e) {
                    if (errorAction != null) {
                        errorAction(e);
                        return;
                    }
                    Logger.LogError("Error getting result: " + e.Message);
                }
            });
        }
        #endregion

        private void OnEnable() {
            if (this.HasStarted == false) {
                return;
            }
            
            this.Reset();

            //For some reason the connection can be messed up on game objects until they are re-enabled
            //We literally fix the problem by turning it off and on again
            foreach (var featureManager in this.ConnectionManager.FeatureManagers) {
                featureManager.gameObject.SetActive(false);
                featureManager.gameObject.SetActive(true);
            }
        }
        private void OnDisable()
        {
            this.ConnectionManager.Disconnect();
        }

        private void OnApplicationQuit()
        {
            Logger.LogInfo("Disconnecting");
            this.ConnectionManager.Disconnect();
        }
    }
}

