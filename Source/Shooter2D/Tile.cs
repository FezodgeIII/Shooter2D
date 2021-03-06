﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using EzGame.Perspective.Planar;
using static EzGame.Perspective.Planar.Textures;

namespace Shooter2D
{
    public struct Tile
    {
        private static Map Map { get { return Game.Map; } }

        public const byte Width = 16, Height = 16;

        public byte Fore, Back;
        public byte Angle;
        public Animation ForeAnimation, BackAnimation;

        public bool Empty { get { return !((Fore > 0) || (Back > 0)); } }
        public bool ForeOnly { get { return ((Fore > 0) && (Back == 0)); } }
        public bool BackOnly { get { return ((Fore == 0) && (Back > 0)); } }
        public bool HasFore { get { return (Fore > 0); } }
        public bool HasBack { get { return (Back > 0); } }

        public void Update(GameTime Time)
        {
            if (HasFore)
                if (ForeAnimation != null) ForeAnimation.Update(Time);
                else if (Mod.Fore[Fore].Frames > 0) ForeAnimation = new Animation(("Tiles.Fore." + Fore + "-"), Mod.Fore[Fore].Frames, true, Mod.Fore[Fore].Speed);
            if (HasBack)
                if (BackAnimation != null) BackAnimation.Update(Time);
                else if (Mod.Back[Back].Frames > 0) BackAnimation = new Animation(("Tiles.Back." + Back + "-"), Mod.Back[Back].Frames, true, Mod.Back[Back].Speed);
        }

        public void Draw(Batch Batch, Vector2 Position, float LayerOffset)
        {
            if (Back > 0)
            {
                Mod.Tile Tile = Mod.Back[Back];
                if (!Tile.Invisible || (Game.State == Game.States.MapEditor))
                {
                    if (Textures.Exists("Tiles.Back." + Back)) Batch.Draw(Textures.Get("Tiles.Back." + Back), Position, null, Color.White, 0, Origin.Center, 1, SpriteEffects.None, (.8f + LayerOffset));
                    if (BackAnimation != null) Batch.Draw(BackAnimation.Texture(), Position, null, Color.White, 0, Origin.Center, 1, SpriteEffects.None, (.75f + LayerOffset));
                }
            }
            if (Fore > 0)
            {
                Mod.Tile Tile = Mod.Fore[Fore];
                if (!Tile.Invisible || (Game.State == Game.States.MapEditor))
                {
                    float Layer = ((Mod.Fore[Fore].Type == Mod.Tile.Types.Wall) ? .4f : (Mod.Fore[Fore].Type == Mod.Tile.Types.Platform) ? .5f : .6f);
                    if (Textures.Exists("Tiles.Fore." + Fore)) Batch.Draw(Textures.Get("Tiles.Fore." + Fore), Position, null, Color.White, MathHelper.ToRadians((Angle / 128f) * 360), Origin.Center, 1, SpriteEffects.None, (Layer + LayerOffset));
                    if (ForeAnimation != null) Batch.Draw(ForeAnimation.Texture(), Position, null, Color.White, MathHelper.ToRadians((Angle / 128f) * 360), Origin.Center, 1, SpriteEffects.None, (Layer - .05f + LayerOffset));
                    if (Mod.Fore[Fore].Border) Batch.Draw(Textures.Get("Tiles.Fore." + Fore + "b"), Position, null, Color.Black, MathHelper.ToRadians((Angle / 128f) * 360), Origin.Center, 1, SpriteEffects.None, (Layer + .05f + LayerOffset));
                }
            }
            //Batch.Draw(Pixel(Color.Black, true), new Rectangle((int)(Position.X - (Width / 2f)), (int)(Position.Y - (Height / 2f)), Width, Height), null, (Color.White * (1 - (Light / 255f))), 0, Origin.None);
            //Batch.DrawString(Light.ToString(), Fonts.Get("Default/Tiny"), (Position - new Vector2(0, 5)), Origin.Center, Color.White, null, 0, .85f);
            //Batch.DrawString(Max.ToString(), Fonts.Get("Default/Tiny"), (Position + new Vector2(0, 5)), Origin.Center, Color.White, null, 0, .85f);
            //Batch.DrawString(State.ToString(), Fonts.Get("Default/Tiny"), Position, Origin.Center, Color.White, null, 0, .85f);
        }
    }
}