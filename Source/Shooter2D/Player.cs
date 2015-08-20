﻿using EzGame;
using EzGame.Collision;
using EzGame.Input;
using EzGame.Perspective.Planar;
using Lidgren.Network;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static EzGame.Perspective.Planar.Textures;

namespace Shooter2D
{
    public class Player : Object
    {
        private static Map Map { get { return Game.Map; } }
        private static Camera Camera { get { return Map.Camera; } }
        private static Player Self => Game.Self;
        private static Player[] Players => Game.Players;

        public bool Collides
        {
            get
            {
                Mask.Position = Position;
                /*for (int x = (int)(Position.X / Tile.Width - 1); x <= (Position.X / Tile.Width + 1); x++)
                    for (int y = (int)(Position.Y / Tile.Height - 1); y <= (Position.Y / Tile.Height + 1); y++)
                        if (World.InBounds(x, y) && (World.Tiles[x, y].Fore > 0) && Tiles.Fore[World.Tiles[x, y].Fore].Solid)
                        {
                            Polygon Mask = Polygon.CreateRectangleWithCross(new Vector2(Tile.Width, Tile.Height), Vector2.Zero);
                            Mask.Position = new Vector2(((x * Tile.Width) + (Tile.Width / 2f)), ((y * Tile.Height) + (Tile.Height / 2f)));
                            if (this.Mask.Intersects(Mask)) return true;
                        }*/
                return false;
            }
        }

        public NetConnection Connection { get; set; }
        public Polygon Mask { get; private set; }
        public string Name;
        public byte Slot;
        public Vector2 Speed = new Vector2(50, 50);

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
            Mask = Polygon.CreateCircle(24, Vector2.Zero);
        }

        public override void Update(GameTime Time)
        {
            if ((Animation == null) || Animation.Finished) ;
            if (this == Self)
            {
                if (Globe.Active)
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
                    Angle = Globe.Lerp(Angle, Globe.Angle(Position, Mouse.CameraPosition), .03f);
                    Camera.Position = Globe.Move(Position, Angle, (Vector2.Distance(Position, Mouse.CameraPosition) / 4));
                    Camera.Angle = Angle;
                }
                if (Timers.Tick("Positions") && (MultiPlayer.Type("Game") == MultiPlayer.Types.Client))
                    MultiPlayer.Send("Game", MultiPlayer.Construct("Game", Game.Packets.Position, Position, Angle),
                        NetDeliveryMethod.UnreliableSequenced, 1);
            }
            base.Update(Time);
        }

        public void Draw()
        {
            Draw(Globe.Batches[0], Position, Color.White, 1, Angle, Origin, Scale, SpriteEffects.None, 0);
        }

        public override void Draw(Batch Batch, Vector2 Position, Color Color, float Opacity, float Angle, Origin Origin,
            float Scale, SpriteEffects Effect = SpriteEffects.None, float Layer = 0)
        {
            Mask.Draw(Color.White, 1);
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