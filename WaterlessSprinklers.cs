using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Waterless Sprinklers Fixed", "Lorenzo, maintained by SeesAll", "1.1.0")]
    [Description("Allows sprinklers to operate without a water connection.")]
    class WaterlessSprinklers : CovalencePlugin
    {
        private const string WaterShortName = "water";
        private const int SprinklerItemId = -781014061;
        private const ulong SprinklerSkinId = 234508;
        private const int RefreshDelay = 2;

        private Configuration _config;
        private ItemDefinition _waterDefinition;
        private readonly HashSet<Sprinkler> _modifiedSprinklers = new HashSet<Sprinkler>();

        #region Configuration

        private class Configuration
        {
            [JsonProperty(PropertyName = "Use permission")]
            public bool UsePermission = false;

            [JsonProperty(PropertyName = "Permission")]
            public string PermissionUse = "waterlesssprinklers.use";

            [JsonProperty(PropertyName = "PermissionAdmin")]
            public string PermissionAdmin = "waterlesssprinklers.admin";

            [JsonProperty(PropertyName = "Make sprinkler special command")]
            public string SetSkinCommand = "setsprinklerspecial";

            [JsonProperty(PropertyName = "Only turn on if there is no IO connected to sprinklers")]
            public bool CheckIoConnection = false;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                {
                    throw new JsonException("Configuration deserialized to null.");
                }
            }
            catch (Exception exception)
            {
                PrintError($"Configuration error: {exception.Message}");
                PrintWarning("Using safe built-in defaults. Waterless mode remains available to all players by default.");
                LoadDefaultConfig();
            }

            ValidateConfig();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void ValidateConfig()
        {
            var defaults = new Configuration();

            if (string.IsNullOrWhiteSpace(_config.PermissionUse))
            {
                PrintWarning($"Invalid use permission; restored '{defaults.PermissionUse}'.");
                _config.PermissionUse = defaults.PermissionUse;
            }

            if (string.IsNullOrWhiteSpace(_config.PermissionAdmin))
            {
                PrintWarning($"Invalid admin permission; restored '{defaults.PermissionAdmin}'.");
                _config.PermissionAdmin = defaults.PermissionAdmin;
            }

            if (string.IsNullOrWhiteSpace(_config.SetSkinCommand))
            {
                PrintWarning($"Invalid special-sprinkler command; restored '{defaults.SetSkinCommand}'.");
                _config.SetSkinCommand = defaults.SetSkinCommand;
            }

            _config.PermissionUse = _config.PermissionUse.Trim();
            _config.PermissionAdmin = _config.PermissionAdmin.Trim();
            _config.SetSkinCommand = _config.SetSkinCommand.Trim();
        }

        #endregion Configuration

        #region Lifecycle

        private void OnServerInitialized()
        {
            _waterDefinition = ItemManager.FindItemDefinition(WaterShortName);
            if (_waterDefinition == null)
            {
                PrintError($"Unable to find the '{WaterShortName}' item definition. The plugin will not modify sprinklers.");
                return;
            }

            permission.RegisterPermission(_config.PermissionUse, this);
            permission.RegisterPermission(_config.PermissionAdmin, this);
            AddCovalenceCommand(_config.SetSkinCommand, nameof(SprinklerSetSkin));

            timer.Once(1f, ReconcileAllSprinklers);
        }

        private void Unload()
        {
            foreach (var sprinkler in _modifiedSprinklers.ToArray())
            {
                RestoreSprinkler(sprinkler);
            }

            _modifiedSprinklers.Clear();
        }

        #endregion Lifecycle

        #region Entity and IO hooks

        private void OnEntitySpawned(Sprinkler sprinkler)
        {
            timer.Once(1f, () => ReconcileSprinkler(sprinkler));
        }

        private void OnEntityKill(Sprinkler sprinkler)
        {
            _modifiedSprinklers.Remove(sprinkler);
        }

        private object OnWireConnect(BasePlayer player, IOEntity entity1, int inputs, IOEntity entity2, int outputs)
        {
            QueueReconcile(entity1);
            QueueReconcile(entity2);
            return null;
        }

        private object OnWireClear(BasePlayer player, IOEntity entity1, int connected, IOEntity entity2, bool flag)
        {
            QueueReconcile(entity1);
            QueueReconcile(entity2);
            return null;
        }

        private void OnIORefCleared(IOEntity.IORef ioRef, IOEntity ioEntity)
        {
            QueueReconcile(ioEntity);
        }

        private void QueueReconcile(IOEntity entity)
        {
            var sprinkler = entity as Sprinkler;
            if (sprinkler != null)
            {
                NextTick(() => ReconcileSprinkler(sprinkler));
            }
        }

        #endregion Entity and IO hooks

        #region Permission hooks

        private void OnUserPermissionGranted(string userId, string permissionName)
        {
            QueuePermissionReconcile(userId, permissionName);
        }

        private void OnUserPermissionRevoked(string userId, string permissionName)
        {
            QueuePermissionReconcile(userId, permissionName);
        }

        private void OnUserGroupAdded(string userId, string groupName)
        {
            if (_config.UsePermission && permission.GroupHasPermission(groupName, _config.PermissionUse))
            {
                QueueOwnerReconcile(userId);
            }
        }

        private void OnUserGroupRemoved(string userId, string groupName)
        {
            if (_config.UsePermission && permission.GroupHasPermission(groupName, _config.PermissionUse))
            {
                QueueOwnerReconcile(userId);
            }
        }

        private void OnGroupPermissionGranted(string groupName, string permissionName)
        {
            QueueGroupPermissionReconcile(permissionName);
        }

        private void OnGroupPermissionRevoked(string groupName, string permissionName)
        {
            QueueGroupPermissionReconcile(permissionName);
        }

        private void QueuePermissionReconcile(string userId, string permissionName)
        {
            if (_config.UsePermission && permissionName == _config.PermissionUse)
            {
                QueueOwnerReconcile(userId);
            }
        }

        private void QueueOwnerReconcile(string userId)
        {
            ulong ownerId;
            if (!ulong.TryParse(userId, out ownerId))
            {
                return;
            }

            NextTick(() => ReconcileOwnerSprinklers(ownerId));
        }

        private void QueueGroupPermissionReconcile(string permissionName)
        {
            if (_config.UsePermission && permissionName == _config.PermissionUse)
            {
                NextTick(ReconcileAllSprinklers);
            }
        }

        #endregion Permission hooks

        #region Commands

        private void SprinklerSetSkin(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (player == null)
            {
                iplayer.Reply("This command can only be used by an in-game player.");
                return;
            }

            if (!iplayer.IsAdmin && !HasAdminPermission(player.userID))
            {
                iplayer.Reply("You do not have permission to use this command.");
                return;
            }

            var item = player.inventory.containerMain.FindItemByItemID(SprinklerItemId)
                       ?? player.inventory.containerBelt.FindItemByItemID(SprinklerItemId);

            if (item == null)
            {
                iplayer.Reply("Sprinkler not found in your main inventory or belt.");
                return;
            }

            item.skin = SprinklerSkinId;
            item.MarkDirty();
            iplayer.Reply("Converted the sprinkler stack to special waterless sprinklers.");
        }

        #endregion Commands

        #region Sprinkler state

        private void ReconcileAllSprinklers()
        {
            if (_waterDefinition == null)
            {
                return;
            }

            foreach (var sprinkler in BaseNetworkable.serverEntities.OfType<Sprinkler>())
            {
                ReconcileSprinkler(sprinkler);
            }
        }

        private void ReconcileOwnerSprinklers(ulong ownerId)
        {
            if (_waterDefinition == null)
            {
                return;
            }

            foreach (var sprinkler in BaseNetworkable.serverEntities.OfType<Sprinkler>())
            {
                if (sprinkler.OwnerID == ownerId)
                {
                    ReconcileSprinkler(sprinkler);
                }
            }
        }

        private void ReconcileSprinkler(Sprinkler sprinkler)
        {
            if (_waterDefinition == null || sprinkler == null || sprinkler.IsDestroyed)
            {
                return;
            }

            var eligible = sprinkler.skinID == SprinklerSkinId || CanUse(sprinkler.OwnerID);
            var shouldBeWaterless = eligible && (!_config.CheckIoConnection || !HasIoConnection(sprinkler));

            if (shouldBeWaterless)
            {
                MakeWaterless(sprinkler);
            }
            else
            {
                RestoreSprinkler(sprinkler);
            }
        }

        private void MakeWaterless(Sprinkler sprinkler)
        {
            sprinkler.SetFuelType(_waterDefinition, null);
            sprinkler.RefreshSprinklerState(RefreshDelay);
            _modifiedSprinklers.Add(sprinkler);
        }

        private void RestoreSprinkler(Sprinkler sprinkler)
        {
            if (sprinkler == null || sprinkler.IsDestroyed || !_modifiedSprinklers.Remove(sprinkler))
            {
                return;
            }

            try
            {
                sprinkler.SetFuelType(null, null);
                sprinkler.RefreshSprinklerState(RefreshDelay);
            }
            catch (Exception exception)
            {
                PrintWarning($"Failed to restore sprinkler state during cleanup: {exception.Message}");
            }
        }

        private static bool HasIoConnection(Sprinkler sprinkler)
        {
            if (sprinkler.inputs != null)
            {
                for (var index = 0; index < sprinkler.inputs.Length; index++)
                {
                    if (sprinkler.inputs[index].connectedTo.ioEnt != null)
                    {
                        return true;
                    }
                }
            }

            if (sprinkler.outputs != null)
            {
                for (var index = 0; index < sprinkler.outputs.Length; index++)
                {
                    if (sprinkler.outputs[index].connectedTo.ioEnt != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool CanUse(ulong ownerId)
        {
            return !_config.UsePermission || permission.UserHasPermission(ownerId.ToString(), _config.PermissionUse);
        }

        private bool HasAdminPermission(ulong playerId)
        {
            return permission.UserHasPermission(playerId.ToString(), _config.PermissionAdmin);
        }

        #endregion Sprinkler state
    }
}
