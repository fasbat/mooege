/*
 * Copyright (C) 2011 mooege project
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 */

using System.Collections.Generic;
using System.Linq;
using Mooege.Common;
using Mooege.Common.MPQ;
using Mooege.Core.GS.Common.Types.Math;
using Mooege.Core.GS.Common.Types.SNO;
using Mooege.Core.GS.Map;
using Mooege.Common.Helpers;
using Mooege.Core.GS.Common.Types.Scene;


namespace Mooege.Core.GS.Generators
{
    public static class WorldGenerator
    {
        static readonly Logger Logger = LogManager.CreateLogger();

        public static World Generate(Game.Game game, int worldSNO)
        {
            if (!MPQStorage.Data.Assets[SNOGroup.Worlds].ContainsKey(worldSNO))
            {
                Logger.Error("Can't find a valid world definition for sno: {0}", worldSNO);
                return null;
            }

            var worldAsset = MPQStorage.Data.Assets[SNOGroup.Worlds][worldSNO];
            var worldData = (Mooege.Common.MPQ.FileFormats.World)worldAsset.Data;

            if (worldData.SceneParams.SceneChunks.Count == 0)
            {
                Logger.Error("World {0} [{1}] is a dynamic world! Can't generate proper dynamic worlds yet!", worldAsset.Name, worldAsset.SNOId);
                return GenerateRandomDungeon(game, worldSNO, worldData);
            }

            var world = new World(game, worldSNO);

            // Create a clusterID => Cluster Dictionary
            var clusters = new Dictionary<int, Mooege.Common.MPQ.FileFormats.SceneCluster>();
            foreach (var cluster in worldData.SceneClusterSet.SceneClusters)
                clusters[cluster.ClusterId] = cluster;

            // Scenes are not aligned to (0, 0) but apparently need to be -farmy
            float minX = worldData.SceneParams.SceneChunks.Min(x => x.PRTransform.Vector3D.X);
            float minY = worldData.SceneParams.SceneChunks.Min(x => x.PRTransform.Vector3D.Y);

            // Count all occurences of each cluster /fasbat
            var clusterCount = new Dictionary<int, int>();

            foreach (var sceneChunk in worldData.SceneParams.SceneChunks)
            {
                var cID = sceneChunk.SceneSpecification.ClusterID;
                if (cID != -1 && clusters.ContainsKey(cID)) // Check for wrong clusters /fasbat
                {
                    if (!clusterCount.ContainsKey(cID))
                        clusterCount[cID] = 0;
                    clusterCount[cID]++;
                }
            }

            // For each cluster generate a list of randomly selected subcenes /fasbat
            var clusterSelected = new Dictionary<int, List<Mooege.Common.MPQ.FileFormats.SubSceneEntry>>();
            foreach (var cID in clusterCount.Keys)
            {
                var selected = new List<Mooege.Common.MPQ.FileFormats.SubSceneEntry>();
                clusterSelected[cID] = selected;
                var count = clusterCount[cID];
                foreach (var group in clusters[cID].SubSceneGroups) // First select from each subscene group /fasbat
                {
                    for (int i = 0; i < group.I0 && count > 0; i++, count--) //TODO Rename I0 to requiredCount? /fasbat
                    {
                        var subSceneEntry = RandomHelper.RandomItem(group.Entries, entry => entry.Probability);
                        selected.Add(subSceneEntry);
                    }

                    if (count == 0)
                        break;
                }

                while (count > 0) // Fill the rest with defaults /fasbat
                {
                    var subSceneEntry = RandomHelper.RandomItem(clusters[cID].Default.Entries, entry => entry.Probability);
                    selected.Add(subSceneEntry);
                    count--;
                }
            }

            foreach (var sceneChunk in worldData.SceneParams.SceneChunks)
            {
                var scene = new Scene(world, sceneChunk.SNOName.SNOId, null);
                scene.MiniMapVisibility = MiniMapVisibility.Visited;
                scene.Position = sceneChunk.PRTransform.Vector3D - new Vector3D(minX, minY, 0);
                scene.RotationAmount = sceneChunk.PRTransform.Quaternion.W;
                scene.RotationAxis = sceneChunk.PRTransform.Quaternion.Vector3D;
                scene.SceneGroupSNO = -1;

                // If the scene has a subscene (cluster ID is set), choose a random subscenes from the cluster load it and attach it to parent scene /farmy
                if (sceneChunk.SceneSpecification.ClusterID != -1)
                {
                    if (!clusters.ContainsKey(sceneChunk.SceneSpecification.ClusterID))
                    {
                        Logger.Warn("Referenced clusterID {0} not found for chunk {1} in world {2}", sceneChunk.SceneSpecification.ClusterID, sceneChunk.SNOName.SNOId, worldSNO);
                    }
                    else
                    {
                        var entries = clusterSelected[sceneChunk.SceneSpecification.ClusterID]; // Select from our generated list /fasbat
                        Mooege.Common.MPQ.FileFormats.SubSceneEntry subSceneEntry = null;

                        if (entries.Count > 0)
                        {
                            subSceneEntry = RandomHelper.RandomItem<Mooege.Common.MPQ.FileFormats.SubSceneEntry>(entries, entry => 1); // TODO Just shuffle the list, dont random every time. /fasbat
                            entries.Remove(subSceneEntry);
                        }
                        else
                            Logger.Error("No SubScenes defined for cluster {0} in world {1}", sceneChunk.SceneSpecification.ClusterID, world.DynamicID);

                        Vector3D pos = FindSubScenePosition(sceneChunk); // TODO According to BoyC, scenes can have more than one subscene, so better enumerate over all subscenepositions /farmy

                        if (pos == null)
                        {
                            Logger.Error("No scene position marker for SubScenes of Scene {0} found", sceneChunk.SNOName.SNOId);
                        }
                        else
                        {
                            Scene subscene = new Scene(world, subSceneEntry.SNOScene, scene);
                            subscene.Position = scene.Position + pos;
                            subscene.MiniMapVisibility = MiniMapVisibility.Visited;
                            subscene.RotationAxis = sceneChunk.PRTransform.Quaternion.Vector3D;
                            subscene.RotationAmount = sceneChunk.PRTransform.Quaternion.W;
                            subscene.Specification = sceneChunk.SceneSpecification;
                            scene.Subscenes.Add(subscene);
                            subscene.LoadActors();
                        }
                    }

                }
                scene.Specification = sceneChunk.SceneSpecification;
                scene.LoadActors();
            }

            return world;
        }

        private static World GenerateRandomDungeon(Game.Game game, int worldSNO, Mooege.Common.MPQ.FileFormats.World worldData)
        {
            var world = new World(game, worldSNO);

            var tiles = worldData.DRLGParams.DRLGTiles;

            var tilesByType = new Dictionary<Mooege.Common.MPQ.FileFormats.TileTypes, List<Mooege.Common.MPQ.FileFormats.TileInfo>>();

            foreach (var tile in tiles)
            {
                if (!tilesByType.ContainsKey(tile.TileType))
                    tilesByType[tile.TileType] = new List<Mooege.Common.MPQ.FileFormats.TileInfo>();
                tilesByType[tile.TileType].Add(tile);
            }

            {
                var entrance = RandomHelper.RandomItem(tilesByType[Mooege.Common.MPQ.FileFormats.TileTypes.Entrance], entry => 1);
                var scene = new Scene(world, entrance.SNOScene, null);
                scene.MiniMapVisibility = MiniMapVisibility.Visited;
                scene.Position = new Vector3D(0, 0, 0);
                scene.RotationAmount = 1.0f;
                scene.RotationAxis = new Vector3D(0, 0, 0);
                scene.SceneGroupSNO = -1;

                var spec = new SceneSpecification();
                scene.Specification = spec;
                spec.Cell = new Vector2D() { X = 0, Y = 0 };
                spec.CellZ = 0;
                spec.SNOLevelAreas = new int[] { 154588, -1, -1, -1 };
                spec.SNOMusic = -1;
                spec.SNONextLevelArea = -1;
                spec.SNONextWorld = -1;
                spec.SNOPresetWorld = -1;
                spec.SNOPrevLevelArea = -1;
                spec.SNOPrevWorld = -1;
                spec.SNOReverb = -1;
                spec.SNOWeather = 50542;
                spec.SNOCombatMusic = -1;
                spec.SNOAmbient = -1;
                spec.ClusterID = -1;
                spec.Unknown1 = 14;
                spec.Unknown3 = 5;
                spec.Unknown4 = -1;
                spec.Unknown5 = 0;
                spec.SceneCachedValues = new SceneCachedValues();
                spec.SceneCachedValues.Unknown1 = 63;
                spec.SceneCachedValues.Unknown2 = 96;
                spec.SceneCachedValues.Unknown3 = 96;
                var sceneFile = MPQStorage.Data.Assets[SNOGroup.Scene][entrance.SNOScene];
                var sceneData = (Mooege.Common.MPQ.FileFormats.Scene)sceneFile.Data;
                spec.SceneCachedValues.AABB1 = sceneData.AABBBounds;
                spec.SceneCachedValues.AABB2 = sceneData.AABBMarketSetBounds;
                spec.SceneCachedValues.Unknown4 = new int[4] { 0, 0, 0, 0 };
                //scene.
                scene.LoadActors();
            }


            return world;
        }

        /// <summary>
        /// Loads all markersets of a scene and looks for the one with the subscene position
        /// </summary>
        private static Vector3D FindSubScenePosition(Mooege.Common.MPQ.FileFormats.SceneChunk sceneChunk)
        {
            var mpqScene = MPQStorage.Data.Assets[SNOGroup.Scene][sceneChunk.SNOName.SNOId].Data as Mooege.Common.MPQ.FileFormats.Scene;

            foreach (var markerSet in mpqScene.MarkerSets)
            {
                var mpqMarkerSet = MPQStorage.Data.Assets[SNOGroup.MarkerSet][markerSet].Data as Mooege.Common.MPQ.FileFormats.MarkerSet;
                foreach (var marker in mpqMarkerSet.Markers)
                    if (marker.Int0 == 16)      // TODO Make this an enum value /farmy
                        return marker.PRTransform.Vector3D;
            }

            return null;
        }
    }
}