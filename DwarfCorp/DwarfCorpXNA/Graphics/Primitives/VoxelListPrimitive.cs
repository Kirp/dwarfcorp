using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DwarfCorp.GameStates;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading;
using System.Collections.Concurrent;

namespace DwarfCorp
{
    /// <summary>
    /// Represents a collection of voxels with a surface mesh. Efficiently culls away
    /// invisible voxels, and properly constructs ramps.
    /// </summary>
    public class VoxelListPrimitive : GeometricPrimitive, IDisposable
    {
        protected readonly bool[] drawFace = new bool[6];
        protected bool isRebuilding = false;
        private readonly Mutex rebuildMutex = new Mutex();
        private static bool StaticsInitialized = false;

        protected void InitializeStatics()
        {
            if(!StaticsInitialized)
            {
                FaceDeltas[(int)BoxFace.Back] = new Vector3(0, 0, 1);
                FaceDeltas[(int)BoxFace.Front] = new Vector3(0, 0, -1);
                FaceDeltas[(int)BoxFace.Left] = new Vector3(-1, 0, 0);
                FaceDeltas[(int)BoxFace.Right] = new Vector3(1, 0, 0);
                FaceDeltas[(int)BoxFace.Top] = new Vector3(0, 1, 0);
                FaceDeltas[(int)BoxFace.Bottom] = new Vector3(0, -1, 0);

                VoxelChunk.CreateFaceDrawMap();

                StaticsInitialized = true;
            }
        }

        public VoxelListPrimitive() :
            base()
        {
            InitializeStatics();
        }


        public static bool IsTopVertex(VoxelVertex v)
        {
            return v == VoxelVertex.BackTopLeft || v == VoxelVertex.FrontTopLeft || v == VoxelVertex.FrontTopRight || v == VoxelVertex.BackTopRight;
        }

        public void InitializeFromChunk(VoxelChunk chunk)
        {
            if (chunk == null)
            {
                return;
            }

            rebuildMutex.WaitOne();

            if (isRebuilding)
            {
                rebuildMutex.ReleaseMutex();
                return;
            }

            isRebuilding = true;
            rebuildMutex.ReleaseMutex();
            int[] ambientValues = new int[4];
            int maxIndex = 0;
            int maxVertex = 0;
            BoxPrimitive bedrockModel = VoxelLibrary.GetPrimitive("Bedrock");

            if (Vertices == null)
            {
                Vertices = new ExtendedVertex[1024];
            }

            if (Indexes == null)
            {
                Indexes = new ushort[512];
            }

            for (int y = 0; y < Math.Min(chunk.Manager.ChunkData.MaxViewingLevel + 1, VoxelConstants.ChunkSizeY); y++)
            {
                for(int x = 0; x < VoxelConstants.ChunkSizeX; x++)
                {
                    for(int z = 0; z < VoxelConstants.ChunkSizeZ; z++)
                    {
                        var v = new TemporaryVoxelHandle(chunk, new LocalVoxelCoordinate(x, y, z));

                        if((v.IsExplored && v.IsEmpty) || !v.IsVisible) continue;

                        BoxPrimitive primitive = VoxelLibrary.GetPrimitive(v.Type);

                        if (v.IsExplored && primitive == null) continue;

                        if (!v.IsExplored)
                        {
                            primitive = bedrockModel;
                        }

                        Color tint = v.Type.Tint;

                        BoxPrimitive.BoxTextureCoords uvs = primitive.UVs;

                        if (v.Type.HasTransitionTextures && v.IsExplored)
                        {
                            uvs = ComputeTransitionTexture(new TemporaryVoxelHandle(v.Chunk.Manager.ChunkData, v.Coordinate));
                        }

                        for (int i = 0; i < 6; i++)
                        {
                            BoxFace face = (BoxFace)i;
                            Vector3 delta = FaceDeltas[i];

                            var faceVoxel = new TemporaryVoxelHandle(chunk.Manager.ChunkData,
                                    chunk.ID + new LocalVoxelCoordinate(x + (int)delta.X, y + (int)delta.Y, z + (int)delta.Z));
                            drawFace[(int)face] = IsFaceVisible(v, faceVoxel, face);
                                                        
                            if (!drawFace[i]) continue;

                            // Let's get the vertex/index positions for the current face.

                            int faceIndex = 0;
                            int faceCount = 0;
                            int vertexIndex = 0;
                            int vertexCount = 0;

                            primitive.GetFace(face, primitive.UVs, out faceIndex, out faceCount, out vertexIndex, out vertexCount);
                            int indexOffset = maxVertex;

                            // Add vertex data to visible voxel faces

                            for (int vertOffset = 0; vertOffset < vertexCount; vertOffset++)
                            {
                                ExtendedVertex vert = primitive.Vertices[vertOffset + vertexIndex];
                                VoxelVertex currentVertex = primitive.Deltas[vertOffset + vertexIndex];

                                VoxelChunk.VertexColorInfo colorInfo = new VoxelChunk.VertexColorInfo();
                                VoxelChunk.CalculateVertexLight(v, currentVertex, chunk.Manager, ref colorInfo);
                                ambientValues[vertOffset] = colorInfo.AmbientColor;
                                Vector3 offset = Vector3.Zero;
                                Vector2 texOffset = Vector2.Zero;

                                if (v.Type.CanRamp && VoxelChunk.ShouldRamp(currentVertex, v.RampType))
                                {
                                    offset = new Vector3(0, -v.Type.RampSize, 0);
                                }

                                if (maxVertex >= Vertices.Length)
                                {
                                    ExtendedVertex[] newVertices = new ExtendedVertex[Vertices.Length * 2];
                                    Vertices.CopyTo(newVertices, 0);
                                    Vertices = newVertices;
                                }

                                Vertices[maxVertex] = new ExtendedVertex(vert.Position + v.WorldPosition +
                                                                   VertexNoise.GetNoiseVectorFromRepeatingTexture(
                                                                       vert.Position + v.WorldPosition) + offset,
                                    new Color(colorInfo.SunColor, colorInfo.AmbientColor, colorInfo.DynamicColor), 
                                    tint,
                                    uvs.Uvs[vertOffset + vertexIndex] + texOffset,
                                    uvs.Bounds[faceIndex / 6]);

                                maxVertex++;
                            }

                            bool flippedQuad = ambientValues[0] + ambientValues[2] >
                                               ambientValues[1] + ambientValues[3];

                            for (int idx = faceIndex; idx < faceCount + faceIndex; idx++)
                            {
                                if (maxIndex >= Indexes.Length)
                                {
                                    ushort[] indexes = new ushort[Indexes.Length * 2];
                                    Indexes.CopyTo(indexes, 0);
                                    Indexes = indexes;
                                }

                                ushort offset = flippedQuad ? primitive.FlippedIndexes[idx] : primitive.Indexes[idx];
                                ushort offset0 = flippedQuad ? primitive.FlippedIndexes[faceIndex] : primitive.Indexes[faceIndex];
                                Indexes[maxIndex] = (ushort)(indexOffset + offset - offset0);
                                maxIndex++;
                            }
                        }
                        // End looping faces
                    }
                }
            }

            MaxIndex = maxIndex;
            MaxVertex = maxVertex;
            GenerateLightmap(chunk.Manager.ChunkData.Tilemap.Bounds);
            isRebuilding = false;

            //chunk.PrimitiveMutex.WaitOne();
            chunk.NewPrimitive = this;
            chunk.NewPrimitiveReceived = true;
            //chunk.PrimitiveMutex.ReleaseMutex();
        }

        private BoxPrimitive.BoxTextureCoords ComputeTransitionTexture(TemporaryVoxelHandle V)
        {
            var type = V.Type;
            var primitive = VoxelLibrary.GetPrimitive(type);

            if (!type.HasTransitionTextures && primitive != null)
                return primitive.UVs;
            else if (primitive == null)
                return null;
            else
            {
                var transition = ComputeTransitions(V.Chunk.Manager.ChunkData, V, type);
                return type.TransitionTextures[transition];
            }
        }

        private static BoxTransition ComputeTransitions(
            ChunkData Data,
            TemporaryVoxelHandle V,
            VoxelType Type)
        {
            if (Type.Transitions == VoxelType.TransitionType.Horizontal)
            {
                var value = ComputeTransitionValueOnPlane(
                    VoxelHelpers.EnumerateManhattanNeighbors2D(V.Coordinate)
                    .Select(c => new TemporaryVoxelHandle(Data, c)), Type);

                return new BoxTransition()
                {
                    Top = (TransitionTexture)value
                };
            }
            else
            {
                var transitionFrontBack = ComputeTransitionValueOnPlane(
                    VoxelHelpers.EnumerateManhattanNeighbors2D(V.Coordinate, ChunkManager.SliceMode.Z)
                    .Select(c => new TemporaryVoxelHandle(Data, c)),
                    Type);

                var transitionLeftRight = ComputeTransitionValueOnPlane(
                    VoxelHelpers.EnumerateManhattanNeighbors2D(V.Coordinate, ChunkManager.SliceMode.X)
                    .Select(c => new TemporaryVoxelHandle(Data, c)),
                    Type);

                return new BoxTransition()
                {
                    Front = (TransitionTexture)transitionFrontBack,
                    Back = (TransitionTexture)transitionFrontBack,
                    Left = (TransitionTexture)transitionLeftRight,
                    Right = (TransitionTexture)transitionLeftRight
                };
            }
        }

        // Todo: Reorder 2d neighbors to make this unecessary.
        private static int[] TransitionMultipliers = new int[] { 2, 8, 4, 1 };

        private static int ComputeTransitionValueOnPlane(IEnumerable<TemporaryVoxelHandle> Neighbors, VoxelType Type)
        {
            int index = 0;
            int accumulator = 0;
            foreach (var v in Neighbors)
            {
                if (v.IsValid && !v.IsEmpty && v.Type == Type)
                    accumulator += TransitionMultipliers[index];
                index += 1;
            }
            return accumulator;
        }

        public void Dispose()
        {
            rebuildMutex.Dispose();
        }
    }

}