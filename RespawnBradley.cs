/*
RespawnBradley Copyright (c) 2021 by PinguinNordpol

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Rust;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Respawn Bradley", "PinguinNordpol", "0.2.0")]
    [Description("Adds the possibility to respawn Bradley via command")]
    class RespawnBradley : CovalencePlugin
    {
        #region Fields
        [PluginReference]
        private Plugin ServerRewards, LootDefender;
        #endregion

        #region Oxide Hooks
        void Init()
        {
            // Register our permissions
            permission.RegisterPermission("respawnbradley.use", this);
            permission.RegisterPermission("respawnbradley.nolock", this);
        }

        void Loaded() => lang.RegisterMessages(Messages, this);

        void OnServerInitialized()
        {
            LoadConfig();
        }
        #endregion

        #region Functions
        /*
         * IsBradleyAlive
         *
         * Check if Bradley is currently alive
         */
        bool IsBradleyAlive()
        {
            BradleySpawner singleton = BradleySpawner.singleton;
            if (singleton != null && (bool)singleton.spawned) return true;

            foreach (HelicopterDebris debris in BaseNetworkable.serverEntities.OfType<HelicopterDebris>())
            {
                string prefab_name = debris?.ShortPrefabName ?? string.Empty;
                if (prefab_name.Contains("bradley"))
                {
                    return true;
                }
            }

            foreach (LockedByEntCrate crate in BaseNetworkable.serverEntities.OfType<LockedByEntCrate>())
            {
                string prefab_name = crate?.ShortPrefabName ?? string.Empty;
                if (prefab_name.Contains("bradley"))
                {
                    return true;
                }
            }

            return false;
        }

        /*
         * DoRespawn
         *
         * Respawn Bradley
         */
        bool DoRespawn(IPlayer player)
        {
            BradleySpawner singleton = BradleySpawner.singleton;

            if (singleton == null)
            {
                Puts("No Bradley spawner found!");
                return false;
            }

            if ((bool)singleton.spawned)
            {
                singleton.spawned.Kill(BaseNetworkable.DestroyMode.None);
            }

            singleton.spawned = null;
            singleton.DoRespawn();

            if (this.configData.Options.LockBradleyOnRespawn && !player.HasPermission("respawnbradley.nolock"))
            {
                if (LootDefender != null)
                {
                    // Telling LootDefender Bradley took max amount of damage, this should hopefully always lock it whatever Damage Lock Threshold has been configured to
                    HitInfo hit_info = new HitInfo(player.Object as BaseEntity, singleton.spawned as BaseEntity, DamageType.Generic, singleton.spawned.MaxHealth(), new Vector3());
                    LootDefender.Call("OnEntityTakeDamage", singleton.spawned, hit_info);
                }
                else
                {
                    Puts("Unable to lock Bradley without LootDefender plugin!");
                }
            }

            return true;
        }

        /*
         * ChargePlayer
         *
         * Charge RP from a player
         */
        bool ChargePlayer(IPlayer player, bool called_by_player)
        {
            object result = null;
            
            if (!called_by_player && !this.configData.Options.ChargeOnServerCommand) return true;
            if (called_by_player && !this.configData.Options.ChargeOnPlayerCommand) return true;

            if (this.configData.Options.UseServerRewards && ServerRewards != null)
            {
                result = ServerRewards.Call("TakePoints", Convert.ToUInt64(player.Id), this.configData.Options.RespawnCosts);
            }
            else
            {
                // No supported rewards plugin loaded or configured
                player.Reply(GetMSG("UnableToCharge", player.Id).Replace("{amount}", this.configData.Options.RespawnCosts.ToString()).Replace("{currency}", this.configData.Options.CurrencySymbol));
                return false;
            }

            if (result == null || (result is bool && (bool)result == false))
            {
                player.Reply(GetMSG("UnableToCharge", player.Id).Replace("{amount}", this.configData.Options.RespawnCosts.ToString()).Replace("{currency}", this.configData.Options.CurrencySymbol));
                return false;
            }

            player.Reply(GetMSG("PlayerCharged", player.Id).Replace("{amount}", this.configData.Options.RespawnCosts.ToString()).Replace("{currency}", this.configData.Options.CurrencySymbol));
            return true;
        }

        /*
         * RefundPlayer
         *
         * Refund RP to a player
         */
        void RefundPlayer(IPlayer player, bool called_by_player)
        {
            object result = null;
            
            if (!called_by_player && !this.configData.Options.RefundOnServerCommand) return;
            if (called_by_player && !this.configData.Options.RefundOnPlayerCommand) return;

            if (this.configData.Options.UseServerRewards && ServerRewards != null)
            {
                result = ServerRewards.Call("AddPoints", Convert.ToUInt64(player.Id), this.configData.Options.RespawnCosts);
            }
            else
            {
                // No supported rewards plugin loaded or configured
                player.Reply(GetMSG("UnableToRefund", player.Id).Replace("{amount}", this.configData.Options.RespawnCosts.ToString()).Replace("{currency}", this.configData.Options.CurrencySymbol));
                return;
            }

            if (result == null || (result is bool && (bool)result == false))
            {
                player.Reply(GetMSG("UnableToRefund", player.Id).Replace("{amount}", this.configData.Options.RespawnCosts.ToString()).Replace("{currency}", this.configData.Options.CurrencySymbol));
                return;
            }

            player.Reply(GetMSG("PlayerRefunded", player.Id).Replace("{amount}", this.configData.Options.RespawnCosts.ToString()).Replace("{currency}", this.configData.Options.CurrencySymbol));
        }
        #endregion

        #region Helpers
        /*
         * FindPlayer
         *
         * Find a player based on steam id
         */
        private IPlayer FindPlayer(string player_id)
        {
            return players.FindPlayerById(player_id);
        }

        /*
         * ColorizeText
         *
         * Replace color placeholders in messages
         */
        private string ColorizeText(string msg)
        {
            return msg.Replace("{MsgCol}", this.configData.Messaging.MsgColor).Replace("{HilCol}", this.configData.Messaging.HilColor).Replace("{ErrCol}", this.configData.Messaging.ErrColor).Replace("{ColEnd}","</color>");
        }
        #endregion

        #region Commands
        /*
         * cmdRespawnBradley
         *
         * Command to respawn Bradley
         */
        [Command("respawnbradley")]
        private void cmdRespawnBradley(IPlayer player, string command, string[] args)
        {
            IPlayer target_player = null;
            bool called_by_player = false;

            if (!player.IsServer && !player.HasPermission("respawnbradley.use"))
            {
                player.Reply(GetMSG("NoPermission", player.Id));
                return;
            }
            else if (!player.IsServer)
            {
                // Player has called command directly
                target_player = player;
                called_by_player = true;
            }
            else
            {
                // Command is called via a store, find target player
                if (args.Length != 1) {
                    // Called via shop, but not given target player id
                    Puts("Erronous invocation of respawnbradley command! Usage: respawnbradley <playerId>");
                    return;
                }

                target_player = this.FindPlayer(args[0]);
                if (target_player == null)
                {
                    // Called via shop, but no valid player id given
                    Puts($"Erronous invocation of respawnbradley command! Unknown player id '{args[0]}'");
                    return;
                }
            }

            // Make sure Bradley is not already alive
            if(this.IsBradleyAlive())
            {
                if (!called_by_player)
                {
                    // If called via shop, player has already been charged, so need to refund here
                    this.RefundPlayer(target_player, called_by_player);
                }
                target_player.Reply(GetMSG("UnableToRespawnBradley", player.Id));
                return;
            }

            // Charge player for respawn
            if (!this.ChargePlayer(target_player, called_by_player))
            {
                return;
            }

            // Respawn Bradley
            if(!this.DoRespawn(target_player))
            {
                this.RefundPlayer(target_player, called_by_player);
                target_player.Reply(GetMSG("UnableToRespawnBradley", player.Id));
                return;
            }

            target_player.Reply(GetMSG("BradleyHasBeenRespawned", player.Id));
        }
        #endregion

        #region Config
        private ConfigData configData;
        class Messaging
        {
            public string MsgColor { get; set; }
            public string HilColor { get; set; }
            public string ErrColor { get; set; }
        }        
        class Options
        {
            public bool LockBradleyOnRespawn { get; set; }
            public bool UseServerRewards { get; set; }
            public bool ChargeOnServerCommand { get; set; }
            public bool ChargeOnPlayerCommand { get; set; }
            public bool RefundOnServerCommand { get; set; }
            public bool RefundOnPlayerCommand { get; set; }
            public int RespawnCosts { get; set; }
            public string CurrencySymbol { get; set; }
        }
        class PluginVersion
        {
            public string CurrentVersion { get; set; }
        }
        class ConfigData
        {
            public Messaging Messaging { get; set; }
            public Options Options { get; set; }
            public Oxide.Core.VersionNumber Version { get; set; }

        }
        private void LoadConfig()
        {
            ConfigData config_data = Config.ReadObject<ConfigData>();

            if (config_data.Version < Version)
            {
                this.configData = this.UpdateConfig(config_data);
            }
            else
            {
                this.configData = config_data;
            }
        }
        private ConfigData CreateNewConfig()
        {
            return new ConfigData
            {
                Messaging = new Messaging
                {
                    MsgColor = "<color=#939393>",
                    HilColor = "<color=orange>",
                    ErrColor = "<color=red>"
                },
                Options = new Options
                {
                    LockBradleyOnRespawn = false,
                    UseServerRewards = true,
                    ChargeOnServerCommand = false,
                    ChargeOnPlayerCommand = false,
                    RefundOnServerCommand = true,
                    RefundOnPlayerCommand = false,
                    RespawnCosts = 10000,
                    CurrencySymbol = "RP"
                },
                Version = Version
            };
        }
        protected override void LoadDefaultConfig() => SaveConfig(this.CreateNewConfig());
        private ConfigData UpdateConfig(ConfigData old_config)
        {
            ConfigData new_config;
            bool config_changed = false;

            if (old_config.Version < new VersionNumber(0, 2, 0))
            {
                new_config = this.CreateNewConfig();
                new_config.Messaging.MsgColor = old_config.Messaging.MsgColor;
                new_config.Messaging.HilColor = old_config.Messaging.HilColor;
                new_config.Messaging.ErrColor = old_config.Messaging.ErrColor;
                new_config.Options.UseServerRewards = old_config.Options.UseServerRewards;
                new_config.Options.ChargeOnServerCommand = old_config.Options.ChargeOnServerCommand;
                new_config.Options.ChargeOnPlayerCommand = old_config.Options.ChargeOnPlayerCommand;
                new_config.Options.RefundOnServerCommand = old_config.Options.RefundOnServerCommand;
                new_config.Options.RefundOnPlayerCommand = old_config.Options.RefundOnPlayerCommand;
                new_config.Options.RespawnCosts = old_config.Options.RespawnCosts;
                new_config.Options.CurrencySymbol = old_config.Options.CurrencySymbol;
                config_changed = true;
            }
            else
            {
                new_config = old_config;
                new_config.Version = Version;
            }

            this.SaveConfig(new_config);
            if (config_changed) Puts("Configuration of RespawnBradley was updated. Please check configuration file for changes!");

            return new_config;
        }
        //private void LoadConfigVariables() => this.configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Messaging
        private string GetMSG(string key, string userid = null) => ColorizeText(lang.GetMessage(key, this, userid));
        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            {"NoPermission", "{ErrCol}You do not have permission to use this command!{ColEnd}"},
            {"UnableToCharge", "{ErrCol}We were unable to charge you {amount} {currency}! Please contact an admin{ColEnd}" },
            {"PlayerCharged", "{MsgCol}You have been charged {ColEnd}{HilCol}{amount} {currency}{ColEnd} {MsgCol}for respawning Bradley{ColEnd}" },
            {"UnableToRefund", "{ErrCol}Unable to refund you {amount} {currency}! Please contact an admin{ColEnd}" },
            {"PlayerRefunded", "{HilCol}You have been refunded {amount} {currency}{ColEnd}" },
            {"UnableToRespawnBradley", "{MsgCol}Unable to respawn Bradley as it's still alive or not all of its debris has been cleared{ColEnd}" },
            {"BradleyHasBeenRespawned", "{HilCol}Bradley has been respawned{ColEnd}" }
        };
        #endregion
    }
}
