// Lamp.cs
// 
//  Modified MIT License (MIT)
//  
//  Copyright (c) 2015 Completely Fair Games Ltd.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// The following content pieces are considered PROPRIETARY and may not be used
// in any derivative works, commercial or non commercial, without explicit 
// written permission from Completely Fair Games:
// 
// * Images (sprites, textures, etc.)
// * 3D Models
// * Sound Effects
// * Music
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DwarfCorp.GameStates;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;

namespace DwarfCorp
{
    [JsonObject(IsReference = true)]
    public class Lamp : Body
    {
        public Lamp()
        {

        }

        private void CreateSpriteStanding()
        {
            SpriteSheet spriteSheet = new SpriteSheet(ContentPaths.Entities.Furniture.interior_furniture);

            List<Point> frames = new List<Point>
            {
                new Point(0, 1),
                new Point(2, 1),
                new Point(1, 1),
                new Point(2, 1)
            };
            Animation lampAnimation = new Animation(GameState.Game.GraphicsDevice, new SpriteSheet(ContentPaths.Entities.Furniture.interior_furniture), "Lamp", 32, 32, frames, true, Color.White, 3.0f, 1f, 1.0f, false);

            var sprite = AddChild(new Sprite(Manager, "sprite", Matrix.Identity, spriteSheet, false)
            {
                LightsWithVoxels = false,
                OrientationType = Sprite.OrientMode.YAxis
            }) as Sprite;
            sprite.AddAnimation(lampAnimation);
            lampAnimation.Play();
        }

        private void CreateSpriteWall(Vector3 diff)
        {
            Vector3 offset = diff * 0.2f + Vector3.Up*0.2f;
            SpriteSheet spriteSheet = new SpriteSheet(ContentPaths.Entities.Furniture.interior_furniture);

            List<Point> frames = new List<Point>
            {
                new Point(5, 0),
            };
            Animation lampAnimation = new Animation(GameState.Game.GraphicsDevice, new SpriteSheet(ContentPaths.Entities.Furniture.interior_furniture), "Lamp", 32, 32, frames, true, Color.White, 3.0f, 1f, 1.0f, false);

            var sprite = AddChild(new Sprite(Manager, "sprite", Matrix.CreateTranslation(offset), spriteSheet, false)
            {
                LightsWithVoxels = false,
                OrientationType = Sprite.OrientMode.YAxis
            }) as Sprite;
            sprite.AddAnimation(lampAnimation);
            lampAnimation.Play();
        }

        private void CreateSprite()
        {
            var voxel = new TemporaryVoxelHandle(Manager.World.ChunkManager.ChunkData,
                GlobalVoxelCoordinate.FromVector3(LocalPosition));
            if (!voxel.IsValid)
            {
                CreateSpriteStanding();
                return;
            }
           
            for (var dx = -1; dx < 2; dx ++)
            {
                for (var dz = -1; dz < 2; dz++)
                {
                    if (Math.Abs(dx) + Math.Abs(dz) != 1)
                        continue;

                    var vox = new TemporaryVoxelHandle(Manager.World.ChunkManager.ChunkData,
                        voxel.Coordinate + new GlobalVoxelOffset(dx, 0, dz));

                    if (vox.IsValid && !vox.IsEmpty)
                    {
                        CreateSpriteWall(new Vector3(dx, 0, dz));
                        return;
                    }
                }
            }

            CreateSpriteStanding();
            return;
        }

        public Lamp(ComponentManager Manager, Vector3 position) :
            base(Manager, "Lamp", Matrix.CreateTranslation(position), new Vector3(1.0f, 1.0f, 1.0f), Vector3.Zero)
        {
            CreateSprite();
            Tags.Add("Lamp");

            var voxelUnder = VoxelHelpers.FindFirstVoxelBelow(new TemporaryVoxelHandle(
                Manager.World.ChunkManager.ChunkData,
                GlobalVoxelCoordinate.FromVector3(position)));
            if (voxelUnder.IsValid)
                AddChild(new VoxelListener(Manager, Manager.World.ChunkManager,
                    voxelUnder));


            AddChild(new LightEmitter(Manager, "light", Matrix.Identity, new Vector3(0.1f, 0.1f, 0.1f), Vector3.Zero, 255, 8)
            {
                HasMoved = true
            });

            CollisionType = CollisionManager.CollisionType.Static;
        }


    }
}
