using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace CustomInteractionMod
{
    public class ModEntry : Mod
    {
        private NPC targetNPC = null;
        private bool waitingForDialogueToEnd = false;

        public override void Entry(IModHelper helper)
        {
            // Event hooks
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
        }

        /// <summary>
        /// Handles the Hugs & Kisses sequencing logic by waiting until all dialogue is clear.
        /// </summary>
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !waitingForDialogueToEnd) return;

            // If the player is currently reading dialogue or in a menu, continue waiting
            if (Game1.activeClickableMenu != null || Game1.currentDialogueCharacter != null)
                return;

            // Dialogue and secondary animation mods are done! Fire our custom framework.
            waitingForDialogueToEnd = false;
            if (targetNPC != null)
            {
                TryTriggerNpcMenu(targetNPC);
                targetNPC = null;
            }
        }

        /// <summary>
        /// Intercepts physical interactions (NPC right-clicks and Bed interactions).
        /// </summary>
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            // Trigger when player inputs an action button (Right Click / A Button)
            if (e.Button.IsActionButton())
            {
                Vector2 grabTile = e.Cursor.GrabTile;

                // 1. BED INTERACTION CHECK (Free Love Override Replacement)
                var tileFeatures = Game1.currentLocation.GetTileFeatureAt((int)grabTile.X, (int)grabTile.Y, "Buildings");
                // Alternatively check property of current tile
                if (Game1.currentLocation.doesTileHaveProperty((int)grabTile.X, (int)grabTile.Y, "Bed", "Back") != null)
                {
                    // Intercept and launch custom marriage bedtime sequence
                    e.Helper.Input.Suppress(e.Button); // Prevent vanilla prompt
                    TryTriggerBedtimeSequence();
                    return;
                }

                // 2. INTERIOR NPC INTERACTION INITIALIZATION
                // Check if we are inside an interior map (not outdoors)
                if (Game1.currentLocation != null && !Game1.currentLocation.IsOutdoors)
                {
                    NPC npc = Game1.currentLocation.isCharacterAtTile(grabTile);
                    if (npc != null && npc.CanSocialize)
                    {
                        // Signal the update ticker to wait for dialogue/other mods to clear
                        targetNPC = npc;
                        waitingForDialogueToEnd = true;
                    }
                }
            }
        }

        /// <summary>
        /// Validates relationship tiers and launches the Valley Girls style yes/no prompt.
        /// </summary>
        private void TryTriggerNpcMenu(NPC npc)
        {
            Friendship friendship = Game1.player.friendshipData.ContainsKey(npc.Name) 
                ? Game1.player.friendshipData[npc.Name] 
                : null;

            if (friendship == null) return;

            int hearts = friendship.Points / 250;
            bool isDating = friendship.IsDating();
            bool isMarried = friendship.IsMarryable() && Game1.player.spouse == npc.Name;

            // Minimum baseline requirement (e.g., must be dating or married)
            if (!isDating && !isMarried && hearts < 8) return;

            // Prompt a Valley Girls style Yes/No question fork
            Response[] yesNoResponses = new Response[]
            {
                new Response("yes", "Yes"),
                new Response("no", "No (Exit)")
            };

            Game1.currentLocation.createQuestionDialogue(
                $"Do you want to spend some time with {npc.DisplayName}?", 
                yesNoResponses, 
                (farmer, answer) => {
                    if (answer == "yes")
                    {
                        // Open up up to 6 choices tiered by relationship level
                        OpenNpcChoiceMenu(npc, hearts, isMarried);
                    }
                }
            );
        }

        /// <summary>
        /// Displays the multi-choice interface matching your second reference point.
        /// </summary>
        private void OpenNpcChoiceMenu(NPC npc, int hearts, bool isMarried)
        {
            List<Response> options = new List<Response>();

            // Base Tier Options (8+ Hearts)
            options.Add(new Response("opt1", "Talk closely"));
            options.Add(new Response("opt2", "Give a massage"));

            // Girlfriend/Boyfriend Tier (10+ Hearts)
            if (hearts >= 10)
            {
                options.Add(new Response("opt3", "Cuddle on the couch"));
            }

            // Marriage Tier Exclusive
            if (isMarried)
            {
                options.Add(new Response("opt4", "Steal a passionate kiss"));
                options.Add(new Response("opt5", "Whisper sweet nothings"));
            }

            // Choice 6 is always the exit option
            options.Add(new Response("exit", "Nevermind (Exit)"));

            // Truncate to maximum of 6 elements if necessary
            var finalChoices = options.Take(6).ToArray();

            Game1.currentLocation.createQuestionDialogue(
                "What would you like to do?",
                finalChoices,
                (farmer, choice) => {
                    if (choice == "exit") return;

                    // Execute corresponding custom animation strings based on selection
                    ExecuteAnimation(npc, choice);
                }
            );
        }

        /// <summary>
        /// Handles Bedtime prompt logic for Polyamory/Free Love spouses who are physically present.
        /// </summary>
        private void TryTriggerBedtimeSequence()
        {
            // Gather all spouses currently in the actual farmhouse/room map
            var presentSpouses = Game1.currentLocation.characters
                .Where(c => c.isVillager() && Game1.player.friendshipData.ContainsKey(c.Name) && Game1.player.friendshipData[c.Name].IsRoommateOrSpouse())
                .ToList();

            if (!presentSpouses.Any())
            {
                // Fallback to regular vanilla sleep menu if no spouses are around
                Game1.currentLocation.showShouldSeeAnswersMenu();
                return;
            }

            Response[] yesNoResponses = new Response[]
            {
                new Response("yes", "Yes"),
                new Response("no", "No, just sleep")
            };

            Game1.currentLocation.createQuestionDialogue(
                "Do you want to have fun with your wife/husband before bed?",
                yesNoResponses,
                (farmer, answer) => {
                    if (answer == "yes")
                    {
                        // Fork out a list selecting which present spouse
                        List<Response> spouseChoices = presentSpouses
                            .Select(s => new Response(s.Name, s.DisplayName))
                            .ToList();
                        
                        spouseChoices.Add(new Response("exit", "Go to sleep"));

                        Game1.currentLocation.createQuestionDialogue(
                            "Which one?",
                            spouseChoices.ToArray(),
                            (f, targetSpouseName) => {
                                if (targetSpouseName == "exit")
                                {
                                    Game1.currentLocation.showShouldSeeAnswersMenu();
                                    return;
                                }

                                NPC spouseNpc = presentSpouses.First(s => s.Name == targetSpouseName);
                                int hearts = (Game1.player.friendshipData[spouseNpc.Name].Points) / 250;
                                
                                // Direct path into their exact custom animation choices
                                OpenNpcChoiceMenu(spouseNpc, hearts, true);
                            }
                        );
                    }
                    else
                    {
                        // Launch the regular vanilla bedtime screen
                        Game1.currentLocation.showShouldSeeAnswersMenu();
                    }
                }
            );
        }

        private void ExecuteAnimation(NPC npc, string choiceKey)
        {
            // Trigger your engine's asset changes or sprite sheet sequences here.
            this.Monitor.Log($"Executing interaction animation '{choiceKey}' on {npc.Name}.", LogLevel.Info);
            
            // Example layout: npc.Sprite.Animate(...) or setting custom state loops
        }
    }
}
