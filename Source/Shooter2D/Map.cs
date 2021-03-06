﻿using Microsoft.Xna.Framework;
using System;
using System.IO;
using System.Collections.Generic;
using EzGame;
using EzGame.Collision;
using EzGame.Perspective.Planar;

namespace Shooter2D
{
    public class Map
    {
        public Tile[,] Tiles;
        public int Width { get { return (Tiles.GetLength(0) * Tile.Width); } }
        public int Height { get { return (Tiles.GetLength(1) * Tile.Height); } }

        public Camera Camera;
        public Vector2 Speed = new Vector2(240, 240);

        public Pathfinder Pathfinder;
        public List<Point>[] Waypoints, Spawns;

        public List<Line> Bullets;

        public Map(int Width, int Height)
        {
            Tiles = new Tile[Width, Height];
            for (int x = 0; x < Width; x++) for (int y = 0; y < Height; y++) Tiles[x, y].Back = 2;
            Camera = new Camera();
            Pathfinder = new Pathfinder(Width, Height);
            Waypoints = new List<Point>[3];
            for (int i = 0; i < Waypoints.Length; i++) Waypoints[i] = new List<Point>();
            Spawns = new List<Point>[3];
            for (int i = 0; i < Spawns.Length; i++) Spawns[i] = new List<Point>();
            Bullets = new List<Line>();
        }

        public void Update(GameTime Time)
        {
            for (int x = (int)((Camera.X - (Screen.ViewportWidth / 2f)) / Tile.Width - 1); x <= (int)((Camera.X + (Screen.ViewportWidth / 2f)) / Tile.Width + 1); x++)
                for (int y = (int)((Camera.Y - (Screen.ViewportHeight / 2f)) / Tile.Height - 1); y <= (int)((Camera.Y + (Screen.ViewportHeight / 2f)) / Tile.Height + 1); y++)
                    if (InBounds(x, y))
                        Tiles[x, y].Update(Time);
        }

        public void Draw() { Draw(Globe.Batches[0]); }
        public void Draw(Batch Batch)
        {
            float LayerOffset = 0;
            for (int y = (int)((Camera.Y - (Screen.ViewportHeight / 2f)) / Tile.Height - 1); y <= (int)((Camera.Y + (Screen.ViewportHeight / 2f)) / Tile.Height + 1); y++)
                for (int x = (int)((Camera.X - (Screen.ViewportWidth / 2f)) / Tile.Width - 1); x <= (int)((Camera.X + (Screen.ViewportWidth / 2f)) / Tile.Width + 1); x++)
                    if (InBounds(x, y))
                    {
                        Tile Tile = Tiles[x, y];
                        Vector2 Position = new Vector2(((x * Tile.Width) + (Tile.Width / 2f)), ((y * Tile.Height) + (Tile.Height / 2f)));
                        Tile.Draw(Batch, Position, LayerOffset);
                        LayerOffset += .000001f;
                    }
            foreach (Line Bullet in Bullets) Bullet.Draw(Batch, Color.White);
        }

        public ushort AFore(Point Point) { Point = (Point + new Point(0, -1)); if (InBounds(Point.X, Point.Y)) return Tiles[Point.X, Point.Y].Fore; else return 0; }
        public ushort RFore(Point Point) { Point = (Point + new Point(1, 0)); if (InBounds(Point.X, Point.Y)) return Tiles[Point.X, Point.Y].Fore; else return 0; }
        public ushort BFore(Point Point) { Point = (Point + new Point(0, 1)); if (InBounds(Point.X, Point.Y)) return Tiles[Point.X, Point.Y].Fore; else return 0; }
        public ushort LFore(Point Point) { Point = (Point + new Point(-1, 0)); if (InBounds(Point.X, Point.Y)) return Tiles[Point.X, Point.Y].Fore; else return 0; }
        public bool PlaceFore(byte ID, int x, int y, byte? Angle = null, bool Self = false)
        {
            if (!InBounds(x, y) || (ID == 0)) return false;
            Mod.Tile Tile = Mod.Fore[ID];
            Point Point = new Point(x, y);
            if (Tile.Type == (Mod.Tile.Types.Platform | Mod.Tile.Types.Wall)) Pathfinder.SetNode(Point, new Pathfinder.Node(false)); else Pathfinder.SetNode(Point, new Pathfinder.Node(true));
            if (Tile.Waypoint.HasValue && !Waypoints[Tile.Waypoint.Value].Contains(Point)) Waypoints[Tile.Waypoint.Value].Add(Point);
            if (Tile.Spawn.HasValue && !Spawns[Tile.Spawn.Value].Contains(Point)) Spawns[Tile.Spawn.Value].Add(Point);
            if (!Angle.HasValue)
            {
                if (Tile.ClipToFore)
                {
                    Angle = 128;
                    ushort RFore = this.RFore(Point), BFore = this.BFore(Point), LFore = this.LFore(Point), AFore = this.AFore(Point);
                    if ((RFore > 0) && Mod.Fore[RFore].Type == (Mod.Tile.Types.Platform | Mod.Tile.Types.Wall)) Angle = 0;
                    else if ((BFore > 0) && Mod.Fore[BFore].Type == (Mod.Tile.Types.Platform | Mod.Tile.Types.Wall)) Angle = 32;
                    else if ((LFore > 0) && Mod.Fore[LFore].Type == (Mod.Tile.Types.Platform | Mod.Tile.Types.Wall)) Angle = 64;
                    else if ((AFore > 0) && Mod.Fore[AFore].Type == (Mod.Tile.Types.Platform | Mod.Tile.Types.Wall)) Angle = 96;
                    if (Angle.Value == 128) return false;
                }
                else Angle = 0;
            }
            if ((Tiles[x, y].Fore == ID) && (Tiles[x, y].Angle == Angle.Value)) return false;
            if (Tile.Frames > 0) Tiles[x, y].ForeAnimation = new Animation(("Tiles.Fore." + ID + "-"), Tile.Frames, true, Tile.Speed);
            else Tiles[x, y].ForeAnimation = null;
            Tiles[x, y].Fore = ID;
            Tiles[x, y].Angle = Angle.Value;
            if (Self && (MultiPlayer.Peer() != null)) MultiPlayer.Send(MultiPlayer.Construct(Game.Packets.PlaceFore, ID, (ushort)x, (ushort)y, Angle));
            return true;
        }
        public bool ClearFore(int x, int y, bool Self = false)
        {
            if (!InBounds(x, y) || !Tiles[x, y].HasFore) return false;
            Mod.Tile Tile = Mod.Fore[Tiles[x, y].Fore];
            Point Point = new Point(x, y);
            Pathfinder.SetNode(Point, new Pathfinder.Node(true));
            if (Tile.Waypoint.HasValue) Waypoints[Tile.Waypoint.Value].Remove(Point);
            if (Tile.Spawn.HasValue) Spawns[Tile.Spawn.Value].Remove(Point);
            Tiles[x, y].Fore = 0;
            Tiles[x, y].Angle = 0;
            Tiles[x, y].ForeAnimation = null;
            if (Self && (MultiPlayer.Peer() != null)) MultiPlayer.Send(MultiPlayer.Construct(Game.Packets.ClearFore, (ushort)x, (ushort)y));
            return true;
        }
        public bool PlaceBack(byte ID, int x, int y, bool Self = false)
        {
            if (!InBounds(x, y) || (ID == 0)) return false;
            Mod.Tile Tile = Mod.Back[ID];
            Point Point = new Point(x, y);
            if (Tiles[x, y].Back == ID) return false;
            if (Tile.Frames > 0) Tiles[x, y].BackAnimation = new Animation(("Tiles.Back." + ID + "-"), Tile.Frames, true, Tile.Speed);
            else Tiles[x, y].BackAnimation = null;
            Tiles[x, y].Back = ID;
            if (Self && (MultiPlayer.Peer() != null)) MultiPlayer.Send(MultiPlayer.Construct(Game.Packets.PlaceBack, ID, (ushort)x, (ushort)y));
            return true;
        }
        public bool ClearBack(int x, int y, bool Self = false)
        {
            if (!InBounds(x, y) || !Tiles[x, y].HasBack) return false;
            Mod.Tile Tile = Mod.Back[Tiles[x, y].Back];
            Tiles[x, y].Back = 0;
            Tiles[x, y].BackAnimation = null;
            if (Self && (MultiPlayer.Peer() != null)) MultiPlayer.Send(MultiPlayer.Construct(Game.Packets.ClearBack, (ushort)x, (ushort)y));
            return true;
        }

        public bool InBounds(int x, int y) { return !((x < 0) || (y < 0) || (x >= Tiles.GetLength(0)) || (y >= Tiles.GetLength(1))); }
        public bool OffCamera(int x, int y, sbyte Offset)
        {
            return ((x < (int)((Camera.X - (Screen.ViewportWidth / 2f)) / Tile.Width - Offset)) || (y < (int)((Camera.Y - (Screen.ViewportHeight / 2f)) / Tile.Height - Offset)) ||
                (x >= (int)((Camera.X + (Screen.ViewportWidth / 2f)) / Tile.Width + Offset)) || (y >= (int)((Camera.Y + (Screen.ViewportHeight / 2f)) / Tile.Height + Offset)));
        }

        public Point GetWaypoint(byte Team) { return Waypoints[Team][Globe.Random(Waypoints[Team].Count - 1)]; }
        public Point GetSpawn(byte Team) { return Spawns[Team][Globe.Random(Spawns[Team].Count - 1)]; }

        public void Save(string Path)
        {
            using (StreamWriter Writer = new StreamWriter(Path))
            {
                Writer.WriteLine("size:" + Tiles.GetLength(0) + "," + Tiles.GetLength(1));
                for (int y = 0; y < Tiles.GetLength(1); y++)
                    for (int x = 0; x < Tiles.GetLength(0); x++)
                        Writer.WriteLine(Tiles[x, y].Fore + "," + Tiles[x, y].Back + "," + Tiles[x, y].Angle);
                Writer.Close();
            }
        }
        public static Map Load(string Path)
        {
            Map Map = null;
            int x = 0, y = 0;
            using (StreamReader Reader = new StreamReader(Path))
            {
                string Line = null;
                while (!string.IsNullOrEmpty(Line = Reader.ReadLine()))
                {
                    string[] Elements = (Line.Contains(":") ? Line.Split(':')[1].Split(',') : Line.Split(','));
                    if (Line.StartsWith("size")) Map = new Map(Convert.ToInt32(Elements[0]), Convert.ToInt32(Elements[1]));
                    else
                    {
                        byte Fore = Convert.ToByte(Elements[0]), Back = Convert.ToByte(Elements[1]);
                        if (Fore > 0) Map.PlaceFore(Fore, x, y, Convert.ToByte(Elements[2]));
                        if (Back > 0) Map.PlaceBack(Back, x, y);
                        if (x == (Map.Tiles.GetLength(0) - 1)) { x = 0; y++; } else x++;
                    }
                }
                Reader.Close();
            }
            return Map;
        }
    }
}