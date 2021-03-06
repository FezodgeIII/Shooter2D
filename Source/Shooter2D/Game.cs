﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using EzGame;
using EzGame.Input;
using EzGame.Perspective.Planar;
using Lidgren.Network;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Screen = EzGame.Screen;
using static EzGame.Perspective.Planar.Textures;

namespace Shooter2D
{
    public class Game : Microsoft.Xna.Framework.Game
    {
        public static float Speed = 1;
        public static States State = States.MainMenu;
        public static Map Map;
        private static Camera Camera { get { return Map.Camera; } }
        public static Player Self;
        public static Player[] Players;
        
        public static string MpName = "Guest";

        public enum GameTypes { Deathmatch, TeamDeathmatch, TeamStandard }
        public static GameTypes GameType = GameTypes.TeamStandard;
        public static double RespawnTimer = 5, RoundTimer, RoundEndWait;
        public enum VictoryStates { Draw, Team, Neutral }
        public static bool RoundEnded = true;
        public static ushort? RoundEndMusicChannel;

        public static byte TeamCount(byte Team) { byte Count = 0; for (byte i = 0; i < Players.Length; i++) if ((Players[i] != null) && (Players[i].Team == Team)) Count++; return Count; }
        public static byte AliveCount(byte Team) { byte Count = 0; for (byte i = 0; i < Players.Length; i++) if ((Players[i] != null) && (Players[i].Team == Team) && !Players[i].Dead) Count++; return Count; }
        public static byte DeadCount(byte Team) { byte Count = 0; for (byte i = 0; i < Players.Length; i++) if ((Players[i] != null) && (Players[i].Team == Team) && Players[i].Dead) Count++; return Count; }

        public static byte EditorForeTile = 1, EditorBackTile = 1;

        public static ulong Version
        {
            get { return Globe.Version; }
            set { Globe.Version = value; }
        }

        public Game()
        {
            Globe.GraphicsDeviceManager = new GraphicsDeviceManager(this)
            {PreferredBackBufferWidth = 960, PreferredBackBufferHeight = 540};
            //AppDomain.CurrentDomain.UnhandledException += (sender, args) => MessageBox.Show(((Exception)args.ExceptionObject).ToString());
        }

        protected override void LoadContent()
        {
            #region Globalization

            Globe.Batches = new Batch[1] {new Batch(GraphicsDevice)};
            Globe.Form = (Form) Control.FromHandle(Window.Handle);
            Globe.GameWindow = Window;
            Globe.ContentManager = Content;
            Globe.GraphicsDevice = GraphicsDevice;
            
            Globe.Viewport = GraphicsDevice.Viewport;
            Globe.GraphicsAdapter = GraphicsDevice.Adapter;
            Globe.TextureLoader = new TextureLoader(Globe.GraphicsDevice);
            Textures.LoadTextures(System.IO.Path.Combine(Globe.ExecutableDirectory, "Content"));

            #endregion

            Screen.Expand(true);
            Sound.Initialize(256);
            Performance.UpdateFramesPerSecondBuffer = new float[180];
            Performance.DrawFramesPerSecondBuffer = new float[3];
            MultiPlayer.Initialize();
            IsMouseVisible = true;

            Mod.Fore = Mod.LoadTiles(@".\Content\Fore.xml");
            Mod.Back = Mod.LoadTiles(@".\Content\Back.xml");
            Mod.Weapons = Mod.LoadWeapons(@".\Content\Weapons.xml");
        }

        protected override void Update(GameTime Time)
        {
            Mouse.Update();
            Keyboard.Update(Time);
            XboxPad.Update(Time);
            Timers.Update(Time);
            Performance.Update(Time);

            Globe.Active = IsActive;
            if (XboxPad.Pressed(XboxPad.Buttons.Back) || Keyboard.Pressed(Keyboard.Keys.Escape)) Exit();

            Profiler.Start("Game Update");
            switch (State)
            {
                #region MainMenu

                case States.MainMenu:
                    if (Globe.Active)
                        if (Keyboard.Pressed(Keyboard.Keys.F1))
                        {
                            CreateLobby("Server");
                            State = States.Game;
                        }
                        else if (Keyboard.Pressed(Keyboard.Keys.F2))
                        {
                            if (Keyboard.Holding(Keyboard.Keys.LeftShift) || Keyboard.Holding(Keyboard.Keys.RightShift))
                                MultiPlayer.Connect("Game", "127.0.0.1", 6121, Globe.Version, MpName);
                            else MultiPlayer.Connect("Game", "71.3.34.68", 6121, Globe.Version, MpName);
                        }
                        else if (Keyboard.Pressed(Keyboard.Keys.F3))
                        {
                            CreateLobby("Server");
                            Camera.Position = new Vector2((Map.Width / 2f), (Map.Height / 2f));
                            State = States.MapEditor;
                        }
                    break;

                #endregion

                case States.RequestMap: if (MultiPlayer.Type() == MultiPlayer.Types.Client) { MultiPlayer.Send(MultiPlayer.Construct(Packets.RequestMap)); State = States.SyncingMap; } break;

                #region MapEditor
                case States.MapEditor:
                    Map.Update(Time);
                    Point MousePoint = new Point((int)(Mouse.CameraPosition.X / Tile.Width), (int)(Mouse.CameraPosition.Y / Tile.Height));
                    if (Globe.Active)
                    {
                        #region Camera Movement
                        if (Keyboard.Holding(Keyboard.Keys.W)) Camera.Position.Y -= (float)(Map.Speed.Y * Time.ElapsedGameTime.TotalSeconds);
                        if (Keyboard.Holding(Keyboard.Keys.S)) Camera.Position.Y += (float)(Map.Speed.Y * Time.ElapsedGameTime.TotalSeconds);
                        if (Keyboard.Holding(Keyboard.Keys.A)) Camera.Position.X -= (float)(Map.Speed.X * Time.ElapsedGameTime.TotalSeconds);
                        if (Keyboard.Holding(Keyboard.Keys.D)) Camera.Position.X += (float)(Map.Speed.X * Time.ElapsedGameTime.TotalSeconds);
                        #endregion
                        bool BackPlace = (Keyboard.Holding(Keyboard.Keys.LeftShift) || Keyboard.Holding(Keyboard.Keys.RightShift));
                        if (Mouse.ScrolledUp()) if (!BackPlace) { if (EditorForeTile > 1) EditorForeTile--; } else { if (EditorBackTile > 1) EditorBackTile--; }
                        if (Mouse.ScrolledDown()) if (!BackPlace) { if (EditorForeTile < Mod.Fore.Values.Count) EditorForeTile++; } else { if (EditorBackTile < Mod.Back.Values.Count) EditorBackTile++; }
                        if (Mouse.Holding(Mouse.Buttons.Left))
                            if (!BackPlace) Map.PlaceFore(EditorForeTile, MousePoint.X, MousePoint.Y, null, true);
                            else Map.PlaceBack(EditorBackTile, MousePoint.X, MousePoint.Y, true);
                        if (Mouse.Holding(Mouse.Buttons.Right))
                            if (!BackPlace) Map.ClearFore(MousePoint.X, MousePoint.Y, true);
                            else Map.ClearBack(MousePoint.X, MousePoint.Y, true);
                    }
                    break;
                #endregion

                #region Game

                case States.Game:
                    Map.Update(Time);
                    for (byte i = 0; i < Players.Length; i++)
                        if (Players[i] != null)
                        {
                            Players[i].Update(Time);
                        }
                    if (MultiPlayer.Type("Game") == MultiPlayer.Types.Server)
                    {
                        if (Timers.Tick("Positions"))
                            foreach (var Player1 in Players)
                                if (Player1 != null && (Player1.Connection != null))
                                {
                                    var O = MultiPlayer.Construct("Game", Packets.Position);
                                    foreach (var Player2 in Players)
                                        if ((Player2 != Player1) && (Player2 != null) && !Player2.Dead)
                                        {
                                            O.Write(Player2.Slot);
                                            O.Write(Player2.Position);
                                            O.Write(Player2.Angle);
                                        }
                                    MultiPlayer.SendTo("Game", O, Player1.Connection, NetDeliveryMethod.UnreliableSequenced, 1);
                                }
                        if (GameType == GameTypes.TeamStandard)
                        {
                            if (!RoundEnded)
                            {
                                if (RoundEndWait > 0) RoundEndWait -= Time.ElapsedGameTime.TotalSeconds;
                                else if ((TeamCount(1) > 0) && (TeamCount(2) > 0))
                                {
                                    if ((DeadCount(1) == TeamCount(1)) && (DeadCount(2) == TeamCount(2))) EndRound(VictoryStates.Draw, 0, 0);
                                    else if (DeadCount(1) == TeamCount(1)) EndRound(VictoryStates.Team, 2, 0);
                                    else if (DeadCount(2) == TeamCount(2)) EndRound(VictoryStates.Team, 1, 0);
                                }
                            }
                            else
                            {
                                if (RoundTimer > 0) RoundTimer -= Time.ElapsedGameTime.TotalSeconds;
                                else NewRound();
                            }
                        }
                    }
                    break;

                    #endregion
            }
            Profiler.Stop("Game Update");

            #region Networking

            MultiPlayer.Flush("Game");
            NetIncomingMessage I;
            while ((I = MultiPlayer.Read("Game")) != null)
            {
                var Break = false;
                switch (I.MessageType)
                {
                    case NetIncomingMessageType.ConnectionApproval:
                        Read(Packets.Connection, I);
                        break;
                    case NetIncomingMessageType.Data:
                        Read((Packets) I.ReadByte(), I);
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        var Status = ((MultiPlayer.Type("Game") == MultiPlayer.Types.Client)
                            ? (NetConnectionStatus) I.ReadByte()
                            : ((MultiPlayer.Type("Game") == MultiPlayer.Types.Server)
                                ? I.SenderConnection.Status
                                : NetConnectionStatus.None));
                        switch (Status)
                        {
                            case NetConnectionStatus.Connected:
                                if (MultiPlayer.Type("Game") == MultiPlayer.Types.Client)
                                {
                                    var Hail = I.SenderConnection.RemoteHailMessage;
                                    Read((Packets) Hail.ReadByte(), Hail);
                                }
                                break;
                            case NetConnectionStatus.Disconnected:
                                if ((MultiPlayer.Type("Game") == MultiPlayer.Types.Client) &&
                                    (I.SenderConnection.Status == NetConnectionStatus.Disconnected))
                                {
                                    QuitLobby();
                                    Break = true;
                                }
                                else Read(Packets.Disconnection, I);
                                break;
                        }
                        break;
                }
                if (Break) break;
            }

            #endregion

            base.Update(Time);
        }

        protected override void Draw(GameTime Time)
        {
            Performance.Draw(Time);
            GraphicsDevice.Clear(Color.CornflowerBlue);

            Profiler.Start("Game Draw");
            switch (State)
            {
                #region MainMenu
                case States.MainMenu:
                    Globe.Batches[0].Draw(Textures.Get("cog"), Screen.ViewportBounds);
                    break;
                #endregion

                #region MapEditor
                case States.MapEditor:
                    Globe.Batches[0].Begin(SpriteSortMode.BackToFront, Camera.View);
                    Map.Draw();
                    Globe.Batches[0].End();
                    Globe.Batches[0].DrawRectangle(new Rectangle(16, (int)((Screen.ViewportHeight / 2f) - (11 * Tile.Height)), 52, (22 * Tile.Height)), (Color.Gray * .75f), (Color.Black * .75f));
                    Vector2 UIPos = new Vector2(32, 0);
                    for (int i = -10; i <= 20; i++)
                    {
                        float Opacity = (1 - (Math.Abs(i) * .05f));
                        UIPos.Y = ((Screen.ViewportHeight / 2f) + (i * (Tile.Height + 5)));
                        ushort ID = (ushort)Math.Max(0, Math.Min(ushort.MaxValue, (EditorForeTile + i)));
                        if ((ID > 0) && (ID <= Mod.Fore.Values.Count))
                        {
                            if (Textures.Exists("Tiles.Fore." + ID)) Textures.Draw(("Tiles.Fore." + ID), UIPos, null, (Color.White * Opacity), 0, Origin.Center, 1);
                            if (Mod.Fore[ID].Frames > 0)
                            {
                                if (Mod.Fore[ID].Animation == null) Mod.Fore[ID].Animation = new Animation(("Tiles.Fore." + ID + "-"), Mod.Fore[ID].Frames, true, Mod.Fore[ID].Speed);
                                else Mod.Fore[ID].Animation.Update(Time);
                                Textures.Draw(Mod.Fore[ID].Animation.Texture(), UIPos, null, (Color.White * Opacity), 0, Origin.Center, 1);
                            }
                        }
                    }
                    UIPos = new Vector2(52, 0);
                    for (int i = -10; i <= 20; i++)
                    {
                        float Opacity = (1 - (Math.Abs(i) * .05f));
                        UIPos.Y = ((Screen.ViewportHeight / 2f) + (i * (Tile.Height + 5)));
                        ushort ID = (ushort)Math.Max(0, Math.Min(ushort.MaxValue, (EditorBackTile + i)));
                        if ((ID > 0) && (ID <= Mod.Back.Values.Count))
                        {
                            if (Textures.Exists("Tiles.Back." + ID)) Textures.Draw(("Tiles.Back." + ID), UIPos, null, (Color.White * Opacity), 0, Origin.Center, 1);
                            if (Mod.Back[ID].Frames > 0)
                            {
                                if (Mod.Back[ID].Animation == null) Mod.Back[ID].Animation = new Animation(("Tiles.Back." + ID + "-"), Mod.Back[ID].Frames, true, Mod.Back[ID].Speed);
                                else Mod.Back[ID].Animation.Update(Time);
                                Textures.Draw(Mod.Back[ID].Animation.Texture(), UIPos, null, (Color.White * Opacity), 0, Origin.Center, 1);
                            }
                        }
                    }
                    break;
                #endregion

                #region Game
                case States.Game:
                    Globe.Batches[0].Begin(SpriteSortMode.BackToFront, Camera.View);
                    Map.Draw();
                    for (byte i = 0; i < Players.Length; i++)
                        if (Players[i] != null)
                        {
                            Players[i].Draw();
                        }
                    Globe.Batches[0].End();
                    break;
                    #endregion
            }
            Profiler.Stop("Game Draw");

            //Performance.Draw(Fonts.Get("Default/ExtraSmall"), new Vector2(5, (Screen.ViewportHeight - 35)), Origin.None, Color.White, Color.Black);
            //Profiler.Draw(430);

            Batch.EndAll();
            base.Draw(Time);
        }

        public static void NewRound()
        {
            RoundTimer = 0; RoundEnded = false;
            for (byte i = 0; i < Players.Length; i++) if (Players[i] != null) Players[i].Dead = false;
            Point Spawn = Map.GetSpawn(Self.Team);
            Self.Respawn(new Vector2(((Spawn.X * Tile.Width) + (Tile.Width / 2f)), ((Spawn.Y * Tile.Height) + (Tile.Height / 2f))));
            if (MultiPlayer.Type() == MultiPlayer.Types.Server) MultiPlayer.Send(MultiPlayer.Construct(Packets.NewRound));
        }
        public static void EndRound(VictoryStates VictoryState, byte TeamWon, byte RoundEndMusicIndex)
        {
            RoundTimer = 10; RoundEnded = true;
            if (MultiPlayer.Type() == MultiPlayer.Types.Server)
            {
                List<object> Details = new List<object>();
                Details.Add((byte)VictoryState);
                if (VictoryState == VictoryStates.Team) Details.Add(TeamWon);
                Details.Add(RoundEndMusicIndex);
                MultiPlayer.Send(MultiPlayer.Construct(Packets.EndRound, Details));
            }
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            if ((Map != null) && (State == States.MapEditor) && MultiPlayer.IsNullOrServer()) Map.Save(@".\map.dat");
            QuitLobby();
            base.OnExiting(sender, args);
        }

        public static bool CreateLobby(string Name)
        {
            if (MultiPlayer.Type("Game") == null)
            {
                //Map = new Map(80, 45);
                Map = Map.Load(@".\map.dat");
                Players = new Player[10];
                Self = Player.Add(new Player(Name));
                MultiPlayer.Start("Game", 6121, Players.Length);
                Timers.Add("Positions", (1/30d));
                return true;
            }
            return false;
        }
        public static void QuitLobby()
        {
            MultiPlayer.Shutdown("Game", string.Empty);
            Players = null;
            Map = null;
            Timers.Remove("Positions");
            State = States.MainMenu;
        }
        public static List<object> GetSyncedMap
        {
            get
            {
                List<object> Details = new List<object>();
                Details.Add(Map.Tiles.GetLength(0)); Details.Add(Map.Tiles.GetLength(1));
                for (int x = 0; x < Map.Tiles.GetLength(0); x++)
                    for (int y = 0; y < Map.Tiles.GetLength(1); y++)
                    {
                        Details.Add(Map.Tiles[x, y].Fore);
                        Details.Add(Map.Tiles[x, y].Back);
                        if (Map.Tiles[x, y].Fore > 0) Details.Add(Map.Tiles[x, y].Angle);
                    }
                return Details;
            }
        }
        public static Map ReadSyncedMap(NetIncomingMessage I)
        {
            Map Map = new Map(I.ReadInt32(), I.ReadInt32());
            for (int x = 0; x < Map.Tiles.GetLength(0); x++)
                for (int y = 0; y < Map.Tiles.GetLength(1); y++)
                {
                    byte Fore = I.ReadByte(), Back = I.ReadByte();
                    if (Fore > 0) Map.PlaceFore(Fore, x, y, I.ReadByte());
                    if (Back > 0) Map.PlaceBack(Back, x, y);
                }
            return Map;
        }
        public static void Read(Packets Packet, NetIncomingMessage I)
        {
            switch (Packet)
            {
                case Packets.Connection:
                    if (MultiPlayer.Type("Game") == MultiPlayer.Types.Server)
                    {
                        var ClientVersion = I.ReadUInt64();
                        if (ClientVersion == Globe.Version)
                        {
                            var Connector = Player.Add(new Player(I.ReadString()) {Connection = I.SenderConnection});
                            if (Connector != null)
                            {
                                if ((GameType == GameTypes.TeamDeathmatch) || (GameType == GameTypes.TeamStandard))
                                    Connector.Team = (byte)((TeamCount(1) > TeamCount(2)) ? 2 : ((TeamCount(2) > TeamCount(1)) ? 1 : Globe.Random(1, 2)));
                                MultiPlayer.Send("Game", MultiPlayer.Construct("Game", Packet, Connector.Slot, Connector.Name, Connector.Team), I.SenderConnection);
                                var Details = new List<object>();
                                Details.Add((byte)GameType); Details.Add(RespawnTimer);
                                Details.Add(Connector.Team);
                                for (byte i = 0; i < Players.Length; i++)
                                    if ((Players[i] != null) && (Players[i] != Connector))
                                    {
                                        Details.Add(true);
                                        Details.Add(Players[i].Name);
                                        Details.Add(Players[i].Team);
                                    }
                                    else Details.Add(false);
                                I.SenderConnection.Approve(MultiPlayer.Construct("Game", Packets.Initial, (byte)Players.Length, Connector.Slot, Details));
                            }
                            else I.SenderConnection.Deny("Full");
                        }
                        else
                            I.SenderConnection.Deny("Version indifference, Client: " + ClientVersion + " - Server: " + Globe.Version);
                    }
                    else if (MultiPlayer.Type("Game") == MultiPlayer.Types.Client)
                    {
                        var Slot = I.ReadByte();
                        Player.Set(Slot, new Player(I.ReadString()) { Team = I.ReadByte() });
                    }
                    break;
                case Packets.Disconnection:
                    var Disconnector = ((MultiPlayer.Type("Game") == MultiPlayer.Types.Server)
                        ? Player.Get(I.SenderConnection)
                        : ((MultiPlayer.Type("Game") == MultiPlayer.Types.Client) ? Players[I.ReadByte()] : null));
                    if (Disconnector != null) Player.Remove(Disconnector);
                    if (MultiPlayer.Type("Game") == MultiPlayer.Types.Server)
                        MultiPlayer.Send("Game", MultiPlayer.Construct("Game", Packets.Disconnection, Disconnector.Slot),
                            I.SenderConnection);
                    break;
                case Packets.Initial:
                    if (MultiPlayer.Type("Game") == MultiPlayer.Types.Client)
                    {
                        Players = new Player[I.ReadByte()];
                        Self = Player.Set(I.ReadByte(), new Player(MpName));
                        GameType = (GameTypes)I.ReadByte(); RespawnTimer = I.ReadDouble();
                        Self.Team = I.ReadByte();
                        for (byte i = 0; i < Players.Length; i++)
                            if (I.ReadBoolean())
                            {
                                Players[i] = new Player(i, I.ReadString())
                                {
                                    Team = I.ReadByte()
                                };
                            }
                        State = States.RequestMap;
                        Timers.Add("Positions", (1/30d));
                    }
                    break;
                case Packets.RequestMap:
                    if (MultiPlayer.Type() == MultiPlayer.Types.Server)
                    {
                        List<object> Details = new List<object>();
                        Details.AddRange(GetSyncedMap);
                        Details.Add((byte)State);
                        MultiPlayer.SendTo(MultiPlayer.Construct(Packet, Details), I.SenderConnection);
                    }
                    else if (MultiPlayer.Type() == MultiPlayer.Types.Client) { Map = ReadSyncedMap(I); State = (States)I.ReadByte(); }
                    break;
                case Packets.Position:
                    var Sender = ((MultiPlayer.Type("Game") == MultiPlayer.Types.Server)
                        ? Player.Get(I.SenderConnection)
                        : null);
                    Vector2 Position;
                    float Angle;
                    if (MultiPlayer.Type("Game") == MultiPlayer.Types.Server)
                    {
                        if (Sender != null)
                        {
                            Sender.Position = I.ReadVector2();
                            Sender.Angle = I.ReadFloat();
                        }
                    }
                    else if (MultiPlayer.Type("Game") == MultiPlayer.Types.Client)
                    {
                        var Count = (byte) ((I.LengthBytes - 1)/12);
                        for (byte i = 0; i < Count; i++)
                        {
                            Sender = Players[I.ReadByte()];
                            Position = I.ReadVector2();
                            Angle = I.ReadFloat();
                            if (Sender != null)
                            {
                                Sender.Position = Position;
                                Sender.Angle = Angle;
                            }
                        }
                    }
                    break;
                case Packets.PlaceFore:
                    byte ID = I.ReadByte(); ushort x = I.ReadUInt16(), y = I.ReadUInt16(); byte TAngle = I.ReadByte();
                    Map.PlaceFore(ID, x, y, TAngle);
                    if (MultiPlayer.Type("Game") == MultiPlayer.Types.Server) MultiPlayer.Send(MultiPlayer.Construct(Packet, ID, x, y, TAngle), I.SenderConnection);
                    break;
                case Packets.ClearFore:
                    x = I.ReadUInt16(); y = I.ReadUInt16();
                    Map.ClearFore(x, y);
                    if (MultiPlayer.Type("Game") == MultiPlayer.Types.Server) MultiPlayer.Send(MultiPlayer.Construct(Packet, x, y), I.SenderConnection);
                    break;
                case Packets.PlaceBack:
                    ID = I.ReadByte(); x = I.ReadUInt16(); y = I.ReadUInt16();
                    Map.PlaceBack(ID, x, y);
                    if (MultiPlayer.Type("Game") == MultiPlayer.Types.Server) MultiPlayer.Send(MultiPlayer.Construct(Packet, ID, x, y), I.SenderConnection);
                    break;
                case Packets.ClearBack:
                    x = I.ReadUInt16(); y = I.ReadUInt16();
                    Map.ClearBack(x, y);
                    if (MultiPlayer.Type("Game") == MultiPlayer.Types.Server) MultiPlayer.Send(MultiPlayer.Construct(Packet, x, y), I.SenderConnection);
                    break;
                case Packets.Fire:
                    Sender = ((MultiPlayer.Type("Game") == MultiPlayer.Types.Server) ? Player.Get(I.SenderConnection) : Players[I.ReadByte()]);
                    Position = I.ReadVector2(); Angle = I.ReadFloat();
                    Sender.Fire(Position, Angle);
                    if (MultiPlayer.Type() == MultiPlayer.Types.Server) MultiPlayer.Send(MultiPlayer.Construct(Packet, Sender.Slot, Position, Angle), I.SenderConnection);
                    break;
                case Packets.Death:
                    Sender = ((MultiPlayer.Type("Game") == MultiPlayer.Types.Server) ? Player.Get(I.SenderConnection) : Players[I.ReadByte()]);
                    Sender.Killer = Players[I.ReadByte()];
                    Sender.Die();
                    if (MultiPlayer.Type() == MultiPlayer.Types.Server) MultiPlayer.Send(MultiPlayer.Construct(Packet, Sender.Slot, Sender.Killer.Slot), I.SenderConnection);
                    break;
                case Packets.Respawn:
                    Sender = ((MultiPlayer.Type("Game") == MultiPlayer.Types.Server) ? Player.Get(I.SenderConnection) : Players[I.ReadByte()]);
                    Position = I.ReadVector2();
                    Sender.Respawn(Position);
                    if (MultiPlayer.Type() == MultiPlayer.Types.Server) MultiPlayer.Send(MultiPlayer.Construct(Packet, Sender.Slot, Position), I.SenderConnection);
                    break;
                case Packets.EndRound:
                    VictoryStates VictoryState = (VictoryStates)I.ReadByte();
                    byte TeamWon = ((VictoryState == VictoryStates.Team) ? I.ReadByte() : (byte)0);
                    byte RoundEndMusicIndex = I.ReadByte();
                    EndRound(VictoryState, TeamWon, RoundEndMusicIndex);
                    break;
                case Packets.NewRound: NewRound(); break;
            }
        }

        public enum Packets
        {
            Connection,
            Disconnection,
            Initial, RequestMap,
            Position,
            PlaceFore, ClearFore, PlaceBack, ClearBack,
            Fire, Death, Respawn,
            EndRound, NewRound, ChangeTeam
        }

        public enum States
        {
            MainMenu,
            Game,
            MapEditor,
            RequestMap,
            SyncingMap
        }
    }
}