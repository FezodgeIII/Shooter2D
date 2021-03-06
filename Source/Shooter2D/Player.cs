﻿using EzGame;
using EzGame.Collision;
using EzGame.Input;
using EzGame.Perspective.Planar;
using Lidgren.Network;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using static EzGame.Perspective.Planar.Textures;

namespace Shooter2D
{
    using GameTypes = Game.GameTypes;
    using Packets = Game.Packets;

    public class Player : Object
    {
        private static GameTypes GameType { get { return Game.GameType; } }
        private static Map Map { get { return Game.Map; } }
        private static Camera Camera { get { return Map.Camera; } }
        private static List<Line> Bullets { get { return Map.Bullets; } }
        private static Player Self => Game.Self;
        private static Player[] Players => Game.Players;

        public bool Collides
        {
            get
            {
                Mask.Position = (Position + new Vector2(0, (4 * Scale)));
                if (Map != null)
                for (int x = (int)(Position.X / Tile.Width - 1); x <= (Position.X / Tile.Width + 1); x++)
                    for (int y = (int)(Position.Y / Tile.Height - 1); y <= (Position.Y / Tile.Height + 1); y++)
                        if (Map.InBounds(x, y) && Map.Tiles[x, y].HasFore &&
                                ((Mod.Fore[Map.Tiles[x, y].Fore].Type == Mod.Tile.Types.Platform) || (Mod.Fore[Map.Tiles[x, y].Fore].Type == Mod.Tile.Types.Wall)))
                        {
                            Polygon Mask = Polygon.CreateRectangle(new Vector2(Tile.Width, Tile.Height), Vector2.Zero);
                            Mask.Position = new Vector2(((x * Tile.Width) + (Tile.Width / 2f)), ((y * Tile.Height) + (Tile.Height / 2f)));
                            if (this.Mask.Intersects(Mask)) return true;
                        }
                return false;
            }
        }

        public NetConnection Connection { get; set; }
        public Polygon Mask { get; private set; }
        public string Name;
        public byte Slot;
        public Vector2 InterpolatedPosition;
        public float InterpolatedAngle;

        public byte Team = 1;
        public float Health;
        public bool Dead = true;
        public Player Killer;
        public double RespawnTimer;
        public void Die()
        {
            if ((this == Self) && (Killer == null)) Killer = Self;
            if (Killer != null)
            {
                Killer.Kills++;
            }
            Game.RoundEndWait = 1;
            Deaths++; Dead = true; RespawnTimer = Game.RespawnTimer;
            if (this == Self)
            {
                if (MultiPlayer.Type() == MultiPlayer.Types.Client) MultiPlayer.Send(MultiPlayer.Construct(Packets.Death, Killer.Slot));
                else if (MultiPlayer.Type() == MultiPlayer.Types.Server) MultiPlayer.Send(MultiPlayer.Construct(Packets.Death, Slot, Killer.Slot));
            }
        }
        public void Respawn(Vector2 Position)
        {
            Killer = null; Health = 1000; Dead = false;
            this.Position = Position; bool T = Collides;
            InterpolatedPosition = this.Position;
            if (this == Self)
            {
                if (MultiPlayer.Type() == MultiPlayer.Types.Client) MultiPlayer.Send(MultiPlayer.Construct(Packets.Respawn, Position));
                else if (MultiPlayer.Type() == MultiPlayer.Types.Server) MultiPlayer.Send(MultiPlayer.Construct(Packets.Respawn, Slot, Position));
            }
        }
        public ushort Kills, Deaths;
        public Vector2 Speed = new Vector2(50, 50);

        public byte Weapon = 0;
        public double FireRate;
        public void Fire(Vector2 Position, float Angle)
        {
            FireRate = (1 / Mod.Weapons[Weapon].RoundsPerSecond);
            Vector2 Start = (Position + Globe.Rotate((Mod.Weapons[Weapon].Bullet * Scale), Angle)), End = Globe.Move(Start, Angle, 2500);
            Line Bullet = new Line(Start, End);
            for (int x = 0; x < Map.Tiles.GetLength(0); x++)
                for (int y = 0; y < Map.Tiles.GetLength(1); y++)
                    if (Map.Tiles[x, y].HasFore && (Mod.Fore[Map.Tiles[x, y].Fore].Type == Mod.Tile.Types.Wall))
                    {
                        Polygon Mask = Polygon.CreateCross(new Vector2(Tile.Width, Tile.Height), Vector2.Zero);
                        Mask.Position = new Vector2(((x * Tile.Width) + (Tile.Width / 2f)), ((y * Tile.Height) + (Tile.Height / 2f)));
                        Vector2 Intersection = Vector2.Zero;
                        if (Mask.Intersects(Bullet, ref Intersection)) Bullet.End = Intersection;
                    }
            if (this == Self)
            {
                if (MultiPlayer.Type() == MultiPlayer.Types.Client) MultiPlayer.Send(MultiPlayer.Construct(Packets.Fire, Position, Angle));
                else if (MultiPlayer.Type() == MultiPlayer.Types.Server) MultiPlayer.Send(MultiPlayer.Construct(Packets.Fire, Slot, Position, Angle));
            }
            else
            {
                for (byte i = 0; i < Players.Length; i++)
                    if ((Players[i] != null) && (Players[i] != this) && (Players[i] != Self) && ((Players[i].Team == 0) || (Team == 0) || (Players[i].Team != Team)))
                    {
                        Vector2 Intersection = Vector2.Zero;
                        if (Players[i].Mask.Intersects(Bullet, ref Intersection)) Bullet.End = Intersection;
                    }
                if (((Team == 0) || (Self.Team == 0) || (Team != Self.Team)) && Self.Mask.Intersects(Bullet)) { Self.Health -= Mod.Weapons[Weapon].Damage; Self.Killer = this; }
            }
            //Bullets.Add(Bullet);
        }

        public Player(string Name)
        {
            this.Name = Name;
            Load();
        }

        public Player(byte Slot, string Name)
        {
            this.Slot = Slot;
            this.Name = Name;
            Load();
        }

        public void Load()
        {
            Scale = .6f;
            Mask = Polygon.CreateCircle((28 * Scale), Vector2.Zero);
            Position = new Vector2((Screen.ViewportWidth / 2f), (Screen.ViewportHeight / 2f));
            bool T = Collides;
        }

        public override void Update(GameTime Time)
        {
            if ((Animation == null) || Animation.Finished) ;
            if (FireRate > 0) FireRate -= Time.ElapsedGameTime.TotalSeconds;
            if (this == Self)
            {
                if (Globe.Active && !Dead)
                {
                    var OldPosition = Position;
                    var Run = (Keyboard.Holding(Keyboard.Keys.LeftShift) || Keyboard.Holding(Keyboard.Keys.RightShift));
                    if (Keyboard.Holding(Keyboard.Keys.W))
                    {
                        if (Keyboard.Holding(Keyboard.Keys.A))
                            Move(new Vector2(-(float) (Speed.X*Time.ElapsedGameTime.TotalSeconds),
                                -(float) (Speed.Y*Time.ElapsedGameTime.TotalSeconds)));
                        else if (Keyboard.Holding(Keyboard.Keys.D))
                            Move(new Vector2((float) (Speed.X*Time.ElapsedGameTime.TotalSeconds),
                                -(float) (Speed.Y*Time.ElapsedGameTime.TotalSeconds)));
                        else Move(new Vector2(0, -(float) (Speed.Y*Time.ElapsedGameTime.TotalSeconds)));
                    }
                    else if (Keyboard.Holding(Keyboard.Keys.S))
                    {
                        if (Keyboard.Holding(Keyboard.Keys.A))
                            Move(new Vector2(-(float) (Speed.X*Time.ElapsedGameTime.TotalSeconds),
                                (float) (Speed.Y*Time.ElapsedGameTime.TotalSeconds)));
                        else if (Keyboard.Holding(Keyboard.Keys.D))
                            Move(new Vector2((float) (Speed.X*Time.ElapsedGameTime.TotalSeconds),
                                (float) (Speed.Y*Time.ElapsedGameTime.TotalSeconds)));
                        else Move(new Vector2(0, (float) (Speed.Y*Time.ElapsedGameTime.TotalSeconds)));
                    }
                    else if (Keyboard.Holding(Keyboard.Keys.A))
                        Move(new Vector2(-(float) (Speed.X*Time.ElapsedGameTime.TotalSeconds), 0));
                    else if (Keyboard.Holding(Keyboard.Keys.D))
                        Move(new Vector2((float) (Speed.X*Time.ElapsedGameTime.TotalSeconds), 0));
                    if (OldPosition != Position)
                    {
                    }
                    Angle = Globe.Lerp(Angle, Globe.Angle(Position, Mouse.CameraPosition), .1f);
                    Camera.Position = Globe.Move(Position, Globe.Angle(Position, Mouse.CameraPosition), (Vector2.Distance(Position, Mouse.CameraPosition) / 4));
                    //Camera.Angle = Angle;
                    if (Mouse.Pressed(Mouse.Buttons.Left) && (FireRate <= 0)) Fire(Position, Angle);
                }
                if (RespawnTimer > 0) RespawnTimer -= Time.ElapsedGameTime.TotalSeconds;
                else if (Dead)
                    if ((GameType == GameTypes.Deathmatch) || (GameType == GameTypes.TeamDeathmatch))
                    {
                        Point Spawn = Map.GetSpawn(Team);
                        Respawn(new Vector2(((Spawn.X * Tile.Width) + (Tile.Width / 2f)), ((Spawn.Y * Tile.Height) + (Tile.Height / 2f))));
                    }
                if ((Health <= 0) && !Dead) Die();
                if (Timers.Tick("Positions") && (MultiPlayer.Type("Game") == MultiPlayer.Types.Client))
                    MultiPlayer.Send("Game", MultiPlayer.Construct("Game", Game.Packets.Position, Position, Angle),
                        NetDeliveryMethod.UnreliableSequenced, 1);
            }
            Globe.Move(ref InterpolatedPosition, Position, (Vector2.Distance(InterpolatedPosition, Position) / 6));
            InterpolatedAngle = Globe.Lerp(InterpolatedAngle, Angle, .125f);
            base.Update(Time);
        }

        public void Draw()
        {
            Draw(Globe.Batches[0], Position, Color.White, 1, Angle, Origin, Scale, SpriteEffects.None, 0);
        }

        public override void Draw(Batch Batch, Vector2 Position, Color Color, float Opacity, float Angle, Origin Origin,
            float Scale, SpriteEffects Effect = SpriteEffects.None, float Layer = 0)
        {
            /*Batch.Draw(Textures.Get("Players.1-" + Mod.Weapons[Weapon].Texture), Position, null, (Color.Black * (Opacity * .65f)), Angle, new Origin(Mod.Weapons[Weapon].Player.X, Mod.Weapons[Weapon].Player.Y),
                Scale, SpriteEffects.None, .425002f);*/
            if (!Dead)
            {
                if (Vector2.Distance(InterpolatedPosition, Position) <= 50)
                {
                    Vector2 ShadowPosition = Globe.Move(InterpolatedPosition, Globe.Angle(new Vector2((Map.Width / 2f), (Map.Height / 2f)), InterpolatedPosition),
                        (4 + (Vector2.Distance(new Vector2((Map.Width / 2f), (Map.Height / 2f)), InterpolatedPosition) / 250)));
                    Batch.Draw(Textures.Get("Players.1-" + Mod.Weapons[Weapon].Texture), ShadowPosition, null, (Color.Black * (Opacity * .35f)), InterpolatedAngle, new Origin(Mod.Weapons[Weapon].Player.X, Mod.Weapons[Weapon].Player.Y),
                        Scale, SpriteEffects.None, .425001f);
                    Batch.Draw(Textures.Get("Players.1-" + Mod.Weapons[Weapon].Texture), InterpolatedPosition, null, (Color * Opacity), InterpolatedAngle, new Origin(Mod.Weapons[Weapon].Player.X, Mod.Weapons[Weapon].Player.Y),
                        Scale, SpriteEffects.None, .425f);
                }
                else
                {
                    Vector2 ShadowPosition = Globe.Move(Position, Globe.Angle(new Vector2((Map.Width / 2f), (Map.Height / 2f)), Position),
                        (4 + (Vector2.Distance(new Vector2((Map.Width / 2f), (Map.Height / 2f)), Position) / 250)));
                    Batch.Draw(Textures.Get("Players.1-" + Mod.Weapons[Weapon].Texture), ShadowPosition, null, (Color.Black * (Opacity * .35f)), Angle, new Origin(Mod.Weapons[Weapon].Player.X, Mod.Weapons[Weapon].Player.Y),
                        Scale, SpriteEffects.None, .425001f);
                    Batch.Draw(Textures.Get("Players.1-" + Mod.Weapons[Weapon].Texture), Position, null, (Color.Black * (Opacity * .65f)), Angle, new Origin(Mod.Weapons[Weapon].Player.X, Mod.Weapons[Weapon].Player.Y),
                       Scale, SpriteEffects.None, .425001f);
                }
            }
            //Mask.Draw(Color.White, 1);
        }

        public void Move(Vector2 Offset)
        {
            float Specific = 1;
            if ((Offset.X != 0) && !Collides)
            {
                Position.X += Offset.X;
                if (Collides)
                {
                    Position.X -= Offset.X;
                    while (!Collides) Position.X += ((Offset.X < 0) ? -Specific : Specific);
                    while (Collides) Position.X -= ((Offset.X < 0) ? -Specific : Specific);
                }
            }
            if ((Offset.Y != 0) && !Collides)
            {
                Position.Y += Offset.Y;
                if (Collides)
                {
                    Position.Y -= Offset.Y;
                    while (!Collides) Position.Y += ((Offset.Y < 0) ? -Specific : Specific);
                    while (Collides) Position.Y -= ((Offset.Y < 0) ? -Specific : Specific);
                }
            }
        }

        public static float VolumeFromDistanceToSelf(Vector2 Position, float FadeUnder, uint MaxUnder)
        {
            if (Self == null) return 0;
            return Self.VolumeFromDistance(Position, FadeUnder, MaxUnder);
        }

        public float VolumeFromDistance(Vector2 Position, float FadeUnder, uint MaxUnder = 0)
        {
            return MathHelper.Clamp(((FadeUnder - (Vector2.Distance(this.Position, Position) - MaxUnder))/FadeUnder), 0,
                1);
        }

        public static Player Get(NetConnection Connection)
        {
            if (Players != null)
                for (byte i = 0; i < Players.Length; i++)
                    if ((Players[i] != null) && (Players[i].Connection == Connection)) return Players[i];
            return null;
        }

        public static Player Add(Player P)
        {
            for (byte i = 0; i < Players.Length; i++)
                if (Players[i] == null)
                {
                    P.Slot = i;
                    Players[i] = P;
                    return P;
                }
            return null;
        }

        public static Player Set(byte Slot, Player P)
        {
            if (Slot < Players.Length)
            {
                P.Slot = Slot;
                Players[Slot] = P;
                return P;
            }
            return null;
        }

        public static bool Remove(Player P)
        {
            for (byte i = 0; i < Players.Length; i++)
                if (Players[i] == P)
                {
                    Players[i] = null;
                    return true;
                }
            return false;
        }
    }
}