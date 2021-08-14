using System;
using BepInEx;
using BepInEx.Configuration;
using UnboundLib;
using HarmonyLib;
using UnityEngine;
using System.Runtime.CompilerServices;
using System.Reflection;
using UnboundLib.Utils.UI;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnboundLib.GameModes;
using System.Linq;
using Photon.Pun;
using UnboundLib.Networking;
using System.Collections.Generic;
using Sonigon;
using System.IO;

namespace PickPhaseShenanigans
{
    [BepInDependency("com.willis.rounds.unbound", BepInDependency.DependencyFlags.HardDependency)]
    //[BepInDependency("io.olavim.rounds.mapsextended", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(ModId, ModName, Version)]
    [BepInProcess("Rounds.exe")]
    public class PickPhaseShenanigans : BaseUnityPlugin
    {
        private static PickPhaseShenanigans instance;
        private static Coroutine disablePlayerCo = null;
        internal static bool shenanigansOngoing = false;
        private static string[] maps;

        private const string ModId = "pykess.rounds.plugins.pickphaseshenanigans";
        private const string ModName = "Pick Phase Shenanigans";
        private const string Version = "0.0.1";

        public static ConfigEntry<bool> PickPhaseMapsConfig;
        public static bool PickPhaseMaps;

        private void Awake()
        {
            instance = this;

            // bind configs with BepInEx
            PickPhaseMapsConfig = Config.Bind("PickPhaseShenanigans", "PickPhaseMaps", false, "Use maps custom made for pick phase shenanigans");
            new Harmony(ModId).PatchAll();
        }
        private void Start()
        {
            // add credits
            Unbound.RegisterCredits("Pick Phase Shenanigans", new string[] { "Pykess" }, new string[] { "github", "Buy me a coffee" }, new string[] { "https://github.com/pdcook/PickPhaseShenanigans", "https://www.buymeacoffee.com/Pykess" });

            // load maps
            PickPhaseShenanigans.maps = Directory.GetFiles(Paths.PluginPath, "pykess-pickphaseshenanigans-map*.map", SearchOption.AllDirectories);
            for (int i = 0; i < PickPhaseShenanigans.maps.Length; i++)
            {
                PickPhaseShenanigans.maps[i] = "0:" + PickPhaseShenanigans.maps[i].Replace(Paths.PluginPath, "");
            }

            // add GUI to modoptions menu
            Unbound.RegisterMenu("Pick Phase Shenanigans", () => { }, this.NewGUI);

            // handshake to sync settings
            Unbound.RegisterHandshake(PickPhaseShenanigans.ModId, this.OnHandShakeCompleted);

            GameModeManager.AddHook(GameModeHooks.HookPickStart, gm => this.PickPhase(gm));
            GameModeManager.AddHook(GameModeHooks.HookPickEnd, gm => this.EndPickPhase(gm));
            GameModeManager.AddHook(GameModeHooks.HookPlayerPickStart, gm => this.PlayerPickPhase(gm));
            GameModeManager.AddHook(GameModeHooks.HookPlayerPickEnd, gm => this.EndPlayerPickPhase(gm));
        }
        private void OnHandShakeCompleted()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                NetworkingManager.RPC_Others(typeof(PickPhaseShenanigans), nameof(SyncSettings), new object[] { PickPhaseShenanigans.PickPhaseMaps });
            }
        }

        [UnboundRPC]
        private static void SyncSettings(bool pickmaps)
        {
            PickPhaseShenanigans.PickPhaseMaps = pickmaps;
        }
        private void NewGUI(GameObject menu)
        {
            MenuHandler.CreateText("Pick Phase Shenanigans Options", menu, out TextMeshProUGUI _);
            MenuHandler.CreateText(" ", menu, out TextMeshProUGUI _, 30);
            MenuHandler.CreateText(" ", menu, out TextMeshProUGUI warningText, 30);
            Toggle PickCheckbox = null;
            void EnabledCheckboxAction(bool flag)
            {

                // FEATURE NOT CURRENTLY SUPPORTED
                if (PickCheckbox != null)
                {
                    PickCheckbox.isOn = false;
                }
                PickPhaseShenanigans.PickPhaseMapsConfig.Value = false;
                PickPhaseShenanigans.PickPhaseMaps = false;

                warningText.text = "PICK PHASE MAPS DEPEND ON A YET-TO-BE RELEASED MOD AND THEREFORE ARE NOT SUPPORTED AT THIS TIME";
                warningText.color = Color.red;

                //PickPhaseShenanigans.PickPhaseMapsConfig.Value = flag;
                //PickPhaseShenanigans.PickPhaseMaps = PickPhaseShenanigans.PickPhaseMapsConfig.Value;
            }
            PickCheckbox = MenuHandler.CreateToggle(PickPhaseShenanigans.PickPhaseMapsConfig.Value, "Pick Phase Specific Maps", menu, EnabledCheckboxAction, 60).GetComponent<Toggle>();
            MenuHandler.CreateText("when enabled, pick phases will use custom maps specifically made for shenanigans", menu, out var _, 30);
            MenuHandler.CreateText(" ", menu, out TextMeshProUGUI _, 30);
        }
        private IEnumerator EndPickPhase(IGameModeHandler gm)
        {
            PickPhaseShenanigans.shenanigansOngoing = false;
            MapManager.instance.LoadNextLevel(false, false);
            yield break;
        }

        private IEnumerator PickPhase(IGameModeHandler gm)
        {
            PickPhaseShenanigans.shenanigansOngoing = true;



            PlayerManager.instance.SetPlayersSimulated(false);
            if (PickPhaseShenanigans.PickPhaseMaps)
            {
                MapManager.instance.GetComponent<PhotonView>().RPC("RPCA_LoadLevel", RpcTarget.All, new object[] { PickPhaseShenanigans.maps[0] });

                yield return new WaitForSecondsRealtime(2f);
                MapTransition.instance.Enter(MapManager.instance.currentMap.Map);
                MapTransition.instance.ClearObjects();
                PlayerManager.instance.RPCA_MovePlayers();
            }
            else
            {
                MapManager.instance.CallInNewMapAndMovePlayers(MapManager.instance.currentLevelID);
            }


            TimeHandler.instance.DoSpeedUp();

            PlayerManager.instance.RevivePlayers();
            PlayerManager.instance.SetPlayersSimulated(true);
            PlayerManager.instance.SetPlayersPlaying(true);

            // TODO:
            /*
             * [ ] Add custom map with floor at bottom or something similar
             * [x] Add some sort of patch to interrupt when a player is about to die and instead freeze them, hide them from the game, and make them involerable so as not to trigger the round over sequence
             * 
             * 
             * 
             */

            yield break;
        }

        private IEnumerator PlayerPickPhase(IGameModeHandler gm)
        {
            PickPhaseShenanigans.disablePlayerCo = Unbound.Instance.StartCoroutine(this.DisablePickingPlayer());

            yield break;
        }
        private IEnumerator EndPlayerPickPhase(IGameModeHandler gm)
        {
            if (PickPhaseShenanigans.disablePlayerCo != null)
            {
                Unbound.Instance.StopCoroutine(PickPhaseShenanigans.disablePlayerCo);
            }

            yield break;
        }
        private IEnumerator DisablePickingPlayer()
        {
            while (true)
            {
                foreach (Player player in PlayerManager.instance.players)
                {
                    if (player.playerID == CardChoice.instance.pickrID && CardChoice.instance.IsPicking)
                    {
                        player.gameObject.SetActive(false);
                    }
                    else if (!player.data.dead)
                    {
                        player.gameObject.SetActive(true);
                    }
                }
                yield return new WaitForSecondsRealtime(0.1f);
            }
            yield break;
        }
    }

    [Serializable]
    [HarmonyPatch(typeof(CardChoice), "SpawnUniqueCard")]
    class CardChoicePatchSpawnUniqueCard
    {
        private static void Postfix(CardChoice __instance, ref GameObject __result)
        {
            if (PickPhaseShenanigans.shenanigansOngoing)
            {
                __result.GetComponentInChildren<DamagableEvent>().GetComponent<Collider2D>().enabled = true;
                /*
                foreach (BoxCollider2D collider in __result.GetComponentsInChildren<UnityEngine.BoxCollider2D>())
                {
                    if (collider.gameObject.name.Contains("Damagable")) { collider.enabled = true; }
                }*/
            }
        }
    }

    [Serializable]
    [HarmonyPatch(typeof(CardChoiceVisuals), "Hide")]
    class CardChoiceVisualsPatchHide
    {
        private static bool Prefix(CardChoiceVisuals __instance)
        {
            if (PickPhaseShenanigans.shenanigansOngoing)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }

    [Serializable]
    [HarmonyPatch(typeof(HealthHandler), "RPCA_Die")]
    class HealthHandlerPatchRPCA_Die
    {
        private static bool Prefix(HealthHandler __instance, Vector2 deathDirection)
        {
            // if the player died but in the shenanigans phase, then don't start the point transition
            if (PickPhaseShenanigans.shenanigansOngoing)
            {

                CharacterData data = (CharacterData)Traverse.Create(__instance).Field("data").GetValue();
                if (!data.isPlaying)
                {
                    return true;
                }
                if (data.dead)
                {
                    return true;
                }
                SoundManager.Instance.Play(__instance.soundDie, __instance.transform);
                data.dead = true;
                if (!__instance.DestroyOnDeath)
                {
                    __instance.gameObject.SetActive(false);
                    GamefeelManager.GameFeel(deathDirection.normalized * 3f);
                    UnityEngine.Object.Instantiate<GameObject>(__instance.deathEffect, __instance.transform.position, __instance.transform.rotation).GetComponent<DeathEffect>().PlayDeath(PlayerSkinBank.GetPlayerSkinColors(data.player.playerID).color, data.playerVel, deathDirection, -1);
                    ((DamageOverTime)Traverse.Create(__instance).Field("dot").GetValue()).StopAllCoroutines();
                    data.stunHandler.StopStun();
                    data.silenceHandler.StopSilence();

                    // do not tell the gamemode that the player has died
                    //PlayerManager.instance.PlayerDied(data.player);

                    return false;
                }
                UnityEngine.Object.Destroy(__instance.transform.root.gameObject);
                return false;
            }

            return true;
        }
    }
}


