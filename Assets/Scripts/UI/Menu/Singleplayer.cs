﻿using Assets.Scripts.Gamemode;
using Assets.Scripts.Gamemode.Settings;
using ExitGames.Client.Photon;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Menu
{
    public class Singleplayer : UiNavigationElement
    {
        public Dropdown LevelDropdown;
        public Dropdown GamemodeDropdown;
        private List<Level> levels;

        private Level selectedLevel;
        private GamemodeSettings selectedGamemode;

        private void Awake()
        {
            levels = LevelBuilder.GetAllLevels();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            PhotonNetwork.Disconnect();
            PhotonNetwork.offlineMode = true;
            LevelDropdown.options = new List<Dropdown.OptionData>();
            foreach (var level in levels)
            {
                LevelDropdown.options.Add(new Dropdown.OptionData(level.Name));
            }
            LevelDropdown.captionText.text = LevelDropdown.options[0].text;
            LevelDropdown.onValueChanged.AddListener(delegate
            {
                var level = levels.Single(x => x.Name == LevelDropdown.captionText.text);
                OnLevelSelected(level);
            });
            GamemodeDropdown.onValueChanged.AddListener(delegate
            {
                var gamemode = selectedLevel.Gamemodes.Single(x => x.Name == GamemodeDropdown.captionText.text
                                                                   || x.GamemodeType.ToString() == GamemodeDropdown.captionText.text);
                OnGamemodeSelected(gamemode);
            });

            OnLevelSelected(levels[0]);
        }

        public override void Back()
        {
            base.Back();
            PhotonNetwork.offlineMode = false;
        }

        public void Create()
        {
            var roomOptions = new RoomOptions
            {
                IsVisible = true,
                IsOpen = true,
                MaxPlayers = 10,
                CustomRoomProperties = new Hashtable
                {
                    { "name", "Singleplayer" },
                    { "level", LevelDropdown.captionText.text },
                    { "gamemode", GamemodeDropdown.captionText.text }
                },
                CustomRoomPropertiesForLobby = new[] { "name", "level", "gamemode" }
            };
            PhotonNetwork.CreateRoom(Guid.NewGuid().ToString(), roomOptions, TypedLobby.Default);
            FengGameManagerMKII.instance.OnJoinedRoom();
            SceneManager.sceneLoaded += SceneLoaded;
        }

        private void OnLevelSelected(Level level)
        {
            selectedLevel = level;
            GamemodeDropdown.options = new List<Dropdown.OptionData>();
            foreach (var gamemode in level.Gamemodes)
            {
                GamemodeDropdown.options.Add(new Dropdown.OptionData(gamemode.Name ?? gamemode.GamemodeType.ToString()));
            }
            GamemodeDropdown.captionText.text = GamemodeDropdown.options[0].text;
        }

        private void OnGamemodeSelected(GamemodeSettings gamemode)
        {
            selectedGamemode = gamemode;
        }

        private void SceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            Canvas.ShowInGameUi();
        }
    }
}