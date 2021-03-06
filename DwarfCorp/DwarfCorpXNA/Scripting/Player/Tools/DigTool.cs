// DigTool.cs
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
    /// <summary>
    /// When using this tool, the player specifies that certain voxels should
    /// be mined.
    /// </summary>
    [JsonObject(IsReference = true)]
    public class DigTool : PlayerTool
    {
        public Color DigDesignationColor { get; set; }
        public Color UnreachableColor { get; set; }
        public float DigDesignationGlowRate { get; set; }

        public override void OnBegin()
        {
           Player.World.Tutorial("mine");
        }

        public override void OnEnd()
        {
            foreach (var des in Player.Faction.DigDesignations)
            {
                Drawer3D.UnHighlightVoxel(des.Value.Vox);
            }
        }


        public override void OnVoxelsSelected(List<TemporaryVoxelHandle> refs, InputManager.MouseButton button)
        {

            if (button == InputManager.MouseButton.Left)
            {
                Player.World.Tutorial("slice");
                List<Task> assignments = new List<Task>();
                foreach (var v in refs)
                {
                    if (!v.IsValid || v.IsEmpty)
                        continue;
                    
                    if(!Player.Faction.IsDigDesignation(v) && !Player.Faction.RoomBuilder.IsInRoom(v))
                    {
                        BuildOrder d = new BuildOrder
                        {
                            Vox = v
                        };
                        Player.Faction.AddDigDesignation(d);
                    }

                    assignments.Add(new KillVoxelTask(v));
                }

                List<CreatureAI> minions = Faction.FilterMinionsWithCapability(Player.SelectedMinions,GameMaster.ToolMode.Dig);
                TaskManager.AssignTasksGreedy(assignments, minions, 5);
                OnConfirm(minions);
            }
            else
            {
                foreach (var r in refs)
                {
                    if (!r.IsValid || r.IsEmpty)
                    {
                        continue;
                    }

                    Player.Faction.RemoveDigDesignation(r);
                }
            }
        }

        public override void OnMouseOver(IEnumerable<Body> bodies)
        {
            throw new NotImplementedException();
        }

        public override void Update(DwarfGame game, DwarfTime time)
        {
            if (Player.IsCameraRotationModeActive())
            {
                Player.VoxSelector.Enabled = false;
                Player.BodySelector.Enabled = false;
                Player.World.SetMouse(null);
                return;
            }

            Player.VoxSelector.Enabled = true;

            if (Player.VoxSelector.VoxelUnderMouse.IsValid && !Player.World.IsMouseOverGui)
            {
                Player.World.ShowTooltip(Player.VoxSelector.VoxelUnderMouse.IsExplored ? Player.VoxSelector.VoxelUnderMouse.Type.Name : "???");
            }

            if (Player.World.IsMouseOverGui)
                Player.World.SetMouse(Player.World.MousePointer);
            else
                Player.World.SetMouse(new Gui.MousePointer("mouse", 1, 1));

            Player.BodySelector.Enabled = false;
            Player.VoxSelector.SelectionType = VoxelSelectionType.SelectFilled;
        }

        public override void Render(DwarfGame game, GraphicsDevice graphics, DwarfTime time)
        {
            foreach (KeyValuePair<ulong, BuildOrder> kvp in Player.Faction.DigDesignations)
            {
                var v = kvp.Value.Vox;

                BoundingBox box = v.GetBoundingBox();


                Color drawColor = DigDesignationColor;

                if (kvp.Value.NumCreaturesAssigned == 0)
                {
                    drawColor = UnreachableColor;
                }

                /*
                drawColor.R = (byte)(drawColor.R * Math.Abs(Math.Sin(time.TotalGameTime.TotalSeconds * DigDesignationGlowRate)) + 50);
                drawColor.G = (byte)(drawColor.G * Math.Abs(Math.Sin(time.TotalGameTime.TotalSeconds * DigDesignationGlowRate)) + 50);
                drawColor.B = (byte)(drawColor.B * Math.Abs(Math.Sin(time.TotalGameTime.TotalSeconds * DigDesignationGlowRate)) + 50);
                Drawer3D.DrawBox(box, drawColor, 0.05f, true);
                 */
                Drawer3D.HighlightVoxel(v, drawColor);
            }
        }

        public override void OnBodiesSelected(List<Body> bodies, InputManager.MouseButton button)
        {
            
        }

        public override void OnVoxelsDragged(List<TemporaryVoxelHandle> voxels, InputManager.MouseButton button)
        {

        }
    }
}
