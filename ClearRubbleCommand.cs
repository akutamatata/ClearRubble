/*  Clear Rubble Command
    Copyright (C) 2018 "Spoffy" - https://www.github.com/Spoffy

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
     */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Eco.Gameplay.Objects;
using Eco.Gameplay.Players;
using Eco.Gameplay.Systems.Chat;
using Eco.Mods.TechTree;
using Eco.Shared.Localization;
using Eco.Shared.Math;
using Eco.Shared.Networking;
using Eco.Shared.Services;
using Eco.Shared.Utils;
using Eco.World.Blocks;

namespace Eco.Mods
{
    public class ClearRubbleCommand : IChatCommandHandler
    {

        class MethodInvocationException : Exception
        {
            public MethodInvocationException(string message) : base(message)
            {
            }
        }
        private static Eco.Shared.Localization.LocString outputMessage;
        public static Eco.Shared.Localization.LocString OutputMessage
        {
            get
            {
                return outputMessage;
            }
            set
            {
                outputMessage = value;
            }
        }

        private static int DestroyRubbleType<T>() where T : RubbleObject
        {
            MethodInfo destroyRubbleMethod = typeof(RubbleObject).GetMethod("Destroy");
            if (destroyRubbleMethod == null)
            {
                throw new MethodInvocationException("Destroy method not found on Rubble type");
            }
            IEnumerable<T> objects = NetObjectManager.GetObjectsOfType<T>();
            var count = 0;
            objects.ForEach(rubbleObject =>
            {
                count += 1;
                destroyRubbleMethod.Invoke(rubbleObject, new Object[0]);
            });
            return count;
        }

        private static int DestroyFallenTrees()
        {
            IEnumerable<TreeEntity> trees = NetObjectManager.GetObjectsOfType<TreeEntity>();
            int count = 0;
            trees.Where((tree) => tree.Fallen).ForEach((tree) =>
            {
                count++;
                //Occasionally a tree may not have a PlantPack - this is a bug in Eco.
                if (tree.PlantPack == null)
                {
                    OutputMessage = new Eco.Shared.Localization.LocString("Warning: Unable to clear a fallen tree it has no PlantPack.Report this to a developer");
                    ChatManager.ServerMessageToAll(OutputMessage, false, DefaultChatTags.Notifications);
                    //LocString = Localizer.Do("Warning: Unable to clear a fallen tree, it has no PlantPack. Report this to a developer.");
                    //ChatManager.ServerMessageToAll(message);
                }
                tree.Destroy();
            });
            return count;
        }

        private static int DestroyWoodDebris()
        {
            var count = 0;
            World.World.TopBlockCache.ForEach((Pos2d, block) =>
            {
                //We need to look one up, as the TopBlockCache contains the top SOLID block, and Wood debris isn't solid.
                var positionAbove = World.World.GetTopPos(Pos2d) + Vector3i.Up;
                var blockAbove = World.World.GetBlock(positionAbove);
                if (blockAbove.Is<TreeDebris>())
                {
                    count++;
                    World.World.DeleteBlock(positionAbove);
                }
            });
            return count;
        }
        //add values here to destroy.
        private static int DestroyRubbleByName(string rubbleName)
        {
            switch (rubbleName.ToLower())
            {
                case "stone":
                    return DestroyRubbleType<RubbleObject<StoneItem>>();
                case "copper":
                    return DestroyRubbleType<RubbleObject<CopperOreItem>>();
                case "gold":
                    return DestroyRubbleType<RubbleObject<GoldOreItem>>();
                case "coal":
                    return DestroyRubbleType<RubbleObject<CoalItem>>();
                case "iron":
                    return DestroyRubbleType<RubbleObject<IronOreItem>>();
                case "fallentrees":
                    return DestroyFallenTrees();
                case "wooddebris":
                    return DestroyWoodDebris();
                //post 8.0
                case "basalt":
                    return DestroyRubbleType<RubbleObject<BasaltItem>>();
                case "gneiss":
                    return DestroyRubbleType<RubbleObject<GneissItem>>();
                case "granite":
                    return DestroyRubbleType<RubbleObject<GraniteItem>>();
                case "limestone":
                    return DestroyRubbleType<RubbleObject<LimestoneItem>>();
                case "sandstone":
                    return DestroyRubbleType<RubbleObject<SandstoneItem>>();
                case "shale":
                    return DestroyRubbleType<RubbleObject<ShaleItem>>();

                case "allstone":
                    return DestroyRubbleType<RubbleObject<StoneItem>>() + DestroyRubbleType<RubbleObject<BasaltItem>>() + DestroyRubbleType<RubbleObject<GneissItem>>() +
                        DestroyRubbleType<RubbleObject<GraniteItem>>() + DestroyRubbleType<RubbleObject<LimestoneItem>>() + DestroyRubbleType<RubbleObject<SandstoneItem>>() +
                        DestroyRubbleType<RubbleObject<ShaleItem>>();
            }

            return -1;
        }

        private static async void DestroyRubbleByNameAndSendResult(string rubbleName, User user)
        {
            var result = await Task.Run(() => DestroyRubbleByName(rubbleName));
            if (result < 0)
            {
                OutputMessage = new Eco.Shared.Localization.LocString("That is not a valid rubble type.");
                //ChatManager.ServerMessageToAll(OutputMessage, false, DefaultChatTags.Notifications);
                ChatManager.ServerMessageToPlayer(OutputMessage, user);
            }
            else
            {
                OutputMessage = new Eco.Shared.Localization.LocString("Destroyed " + result + " rubble");
                ChatManager.ServerMessageToPlayer(OutputMessage, user);
            }
        }



        [ChatCommand("Clears all of a type of debris from the server. Valid debris types are 'stone', 'copper', 'gold', 'coal', 'iron', 'fallentrees', 'wooddebris', 'basalt', 'gneiss', 'granite', " +
            "'limestone', 'sandstone', 'shale', and 'allStone'", ChatAuthorizationLevel.Admin)]
        public static void ClearRubbleType(User user, string rubbleName = "")
        {

            if (rubbleName.Length == 0)
            {
                OutputMessage = new Eco.Shared.Localization.LocString("You need to specify an item type");
                ChatManager.ServerMessageToPlayer(OutputMessage, user);
                return;
            }

            try
            {
                DestroyRubbleByNameAndSendResult(rubbleName, user);
            }
            catch (MethodInvocationException ex)
            {
                OutputMessage = new Eco.Shared.Localization.LocString("Error occured: " + ex.Message);
                ChatManager.ServerMessageToPlayer(OutputMessage, user);
            }
        }

    }
}