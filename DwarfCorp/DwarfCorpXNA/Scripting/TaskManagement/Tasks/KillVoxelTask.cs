﻿// KillVoxelTask.cs
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

namespace DwarfCorp
{
    /// <summary>
    /// Tells a creature that it should destroy a voxel via digging.
    /// </summary>
    [Newtonsoft.Json.JsonObject(IsReference = true)]
    internal class KillVoxelTask : Task
    {
        public TemporaryVoxelHandle VoxelToKill = TemporaryVoxelHandle.InvalidHandle;

        public KillVoxelTask()
        {
            Priority = PriorityType.Medium;
        }

        public KillVoxelTask(TemporaryVoxelHandle vox)
        {
            Name = "Mine Block " + vox.Coordinate;
            VoxelToKill = vox;
            Priority = PriorityType.Low;
        }

        public override Task Clone()
        {
            return new KillVoxelTask(VoxelToKill);
        }

        public override Act CreateScript(Creature creature)
        {
            return new KillVoxelAct(VoxelToKill, creature.AI);
        }

        public override bool ShouldRetry(Creature agent)
        {
            return !VoxelToKill.IsEmpty && agent.Faction.IsDigDesignation(VoxelToKill);
        }

        public override bool IsFeasible(Creature agent)
        {
            if(!VoxelToKill.IsValid || VoxelToKill.IsEmpty || VoxelToKill.Health <= 0)
                return false;

            return agent.Faction.IsDigDesignation(VoxelToKill) && !VoxelHelpers.VoxelIsCompletelySurrounded(VoxelToKill);
        }

        public override bool ShouldDelete(Creature agent)
        {
            return !VoxelToKill.IsValid || VoxelToKill.IsEmpty || VoxelToKill.Health <= 0 ||
                   !agent.Faction.IsDigDesignation(VoxelToKill);
        }

        public override float ComputeCost(Creature agent, bool alreadyCheckedFeasible = false)
        {
            if (!VoxelToKill.IsValid)
                return 1000;

            int surroundedValue = 0;
            if (!alreadyCheckedFeasible)
            {
                if (VoxelHelpers.VoxelIsCompletelySurrounded(VoxelToKill))
                    surroundedValue = 10000;
            }

            return (agent.AI.Position - VoxelToKill.WorldPosition).LengthSquared() + 100 * Math.Abs(agent.AI.Position.Y - VoxelToKill.Coordinate.Y) + surroundedValue;
        }
    }

}