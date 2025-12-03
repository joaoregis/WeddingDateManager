using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using System.Diagnostics.CodeAnalysis;

namespace WeddingDateManager
{
    public class WeddingMenu : IClickableMenu
    {
        private readonly IModHelper Helper;
        private ClickableTextureComponent? btnPostpone;
        private ClickableTextureComponent? btnAdvance;
        private ClickableTextureComponent btnClose;
        private string statusText = "Calculando...";
        private Friendship? targetFriendship = null;
        private Farmer? engagedPlayer = null;
        private string spouseName = "";
        private bool canModifyDate = false;
        private bool isPlayerWedding = false;

        public WeddingMenu(IModHelper helper)
            : base(Game1.viewport.Width / 2 - 400, Game1.viewport.Height / 2 - 300, 800, 600, false)
        {
            this.Helper = helper;
            this.IdentifyTarget();
            this.SetupComponents();
            this.RecalculateStatus();
        }

        private void IdentifyTarget()
        {
            foreach (Farmer farmer in Game1.getAllFarmers())
            {
                foreach (var key in farmer.friendshipData.Keys)
                {
                    var friendship = farmer.friendshipData[key];
                    if (friendship.Status == FriendshipStatus.Engaged && friendship.WeddingDate != null)
                    {
                        this.targetFriendship = friendship;
                        this.engagedPlayer = farmer;

                        if (long.TryParse(key, out long spousePlayerID))
                        {
                            Farmer? spousePlayer = Game1.GetPlayer(spousePlayerID);
                            this.spouseName = spousePlayer?.Name ?? "Desconhecido";
                        }
                        else
                        {
                            NPC? spouseNPC = Game1.getCharacterFromName(key);
                            this.spouseName = spouseNPC?.displayName ?? key;
                        }

                        goto Found;
                    }
                }
            }

            try
            {
                var teamObj = Game1.player.team;
                var friendshipDataField = this.Helper.Reflection.GetField<object>(teamObj, "friendshipData", false);
                if (friendshipDataField != null)
                {
                    dynamic friendshipData = friendshipDataField.GetValue();
                    foreach (var key in friendshipData.Keys)
                    {
                        var friendship = friendshipData[key];
                        if (friendship.Status.ToString() == "Engaged" && friendship.WeddingDate != null)
                        {
                            this.targetFriendship = friendship;
                            this.isPlayerWedding = true;

                            try
                            {
                                long id1 = key.Farmer1;
                                long id2 = key.Farmer2;
                                Farmer f1 = Game1.GetPlayer(id1);
                                Farmer f2 = Game1.GetPlayer(id2);

                                this.engagedPlayer = f1;
                                this.spouseName = f2?.Name ?? "Outro Jogador";
                            }
                            catch
                            {
                                this.spouseName = "Outro Jogador";
                            }

                            this.canModifyDate = Game1.IsMasterGame ||
                                                 (this.engagedPlayer != null && Game1.player == this.engagedPlayer) ||
                                                 (this.spouseName == Game1.player.Name);

                            if (!this.canModifyDate) this.canModifyDate = true;

                            goto Found;
                        }
                    }
                }
            }
            catch { }

        Found:
            if (this.targetFriendship != null)
            {
                if (!this.isPlayerWedding)
                {
                    this.canModifyDate = Game1.IsMasterGame || Game1.player == this.engagedPlayer;
                }
            }
            else
            {
                this.canModifyDate = false;
                this.statusText = "Nenhum casamento ativo encontrado.";
            }
        }

        [MemberNotNull(nameof(btnClose))]
        private void SetupComponents()
        {
            int centerX = this.xPositionOnScreen + (this.width / 2);
            int centerY = this.yPositionOnScreen + (this.height / 2);

            if (this.canModifyDate)
            {
                this.btnAdvance = new ClickableTextureComponent(
                    new Rectangle(centerX - 96, centerY + 96, 48, 44),
                    Game1.mouseCursors,
                    new Rectangle(352, 495, 12, 11),
                    4f
                )
                { myID = 101, label = "" };

                this.btnPostpone = new ClickableTextureComponent(
                    new Rectangle(centerX + 48, centerY + 96, 48, 44),
                    Game1.mouseCursors,
                    new Rectangle(365, 495, 12, 11),
                    4f
                )
                { myID = 100, label = "" };
            }

            this.btnClose = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + this.width - 36, this.yPositionOnScreen - 8, 48, 48),
                Game1.mouseCursors,
                new Rectangle(337, 494, 12, 12),
                4f
            );
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (this.canModifyDate && this.btnPostpone != null && this.btnPostpone.containsPoint(x, y))
            {
                UpdateDate(1);
                Game1.playSound("drumkit6");
            }
            else if (this.canModifyDate && this.btnAdvance != null && this.btnAdvance.containsPoint(x, y))
            {
                UpdateDate(-1);
                Game1.playSound("drumkit6");
            }
            else if (this.btnClose.containsPoint(x, y))
            {
                this.exitThisMenu();
                Game1.playSound("bigDeSelect");
            }
        }

        private void UpdateDate(int daysToAdd)
        {
            if (this.targetFriendship != null && this.targetFriendship.WeddingDate != null)
            {
                UpdateDateLogic(this.targetFriendship.WeddingDate, daysToAdd, (newDate) =>
                {
                    this.targetFriendship.WeddingDate = newDate;
                });
            }
            else
            {
                Game1.showRedMessage("Nenhum casamento encontrado para modificar.");
            }
        }

        private void UpdateDateLogic(WorldDate currentDate, int daysToAdd, Action<WorldDate> updateAction)
        {
            int currentTotalDays = currentDate.TotalDays;
            int newTotalDays = currentTotalDays + daysToAdd;

            if (newTotalDays <= Game1.Date.TotalDays)
            {
                Game1.showRedMessage("Nao e possivel agendar para hoje ou passado!");
                return;
            }

            int newYear = 1 + (newTotalDays / 112);
            int remainderYear = newTotalDays % 112;
            int newSeasonIndex = remainderYear / 28;
            int newDay = (remainderYear % 28) + 1;

            WorldDate newDate = new WorldDate(newYear, GetSeasonFromIndex(newSeasonIndex), newDay);

            updateAction(newDate);
            RecalculateStatus();
        }

        private string GetSeasonFromIndex(int index)
        {
            return index switch
            {
                0 => "spring",
                1 => "summer",
                2 => "fall",
                3 => "winter",
                _ => "spring"
            };
        }

        private void RecalculateStatus()
        {
            if (this.targetFriendship?.WeddingDate != null)
            {
                int daysRemaining = this.targetFriendship.WeddingDate.TotalDays - Game1.Date.TotalDays;
                string dateString = Utility.getDateStringFor(
                    this.targetFriendship.WeddingDate.DayOfMonth,
                    this.targetFriendship.WeddingDate.SeasonIndex,
                    this.targetFriendship.WeddingDate.Year
                );
                this.statusText = $"Dias restantes: {daysRemaining}\n({dateString})";
            }
            else
            {
                this.statusText = "Nenhum casamento ativo.";
            }
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);
            Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);

            string title = "Gerenciar Casamento";
            int titleWidth = SpriteText.getWidthOfString(title);
            int titleX = this.xPositionOnScreen + (this.width / 2) - (titleWidth / 2);

            SpriteText.drawStringWithScrollBackground(b, title, titleX, this.yPositionOnScreen - 20);

            if (this.engagedPlayer != null)
            {
                string coupleText = $"Casamento de: {this.engagedPlayer.Name} & {this.spouseName}";
                Vector2 coupleSize = Game1.dialogueFont.MeasureString(coupleText);
                Vector2 couplePos = new Vector2(
                    this.xPositionOnScreen + (this.width / 2) - (coupleSize.X / 2),
                    this.yPositionOnScreen + 128
                );
                b.DrawString(Game1.dialogueFont, coupleText, couplePos, Game1.textColor);
            }
            else if (this.isPlayerWedding)
            {
                string coupleText = "Casamento de Jogadores";
                Vector2 coupleSize = Game1.dialogueFont.MeasureString(coupleText);
                Vector2 couplePos = new Vector2(
                   this.xPositionOnScreen + (this.width / 2) - (coupleSize.X / 2),
                   this.yPositionOnScreen + 128
               );
                b.DrawString(Game1.dialogueFont, coupleText, couplePos, Game1.textColor);
            }

            if (!string.IsNullOrEmpty(this.statusText))
            {
                Vector2 textSize = Game1.dialogueFont.MeasureString(this.statusText);
                Vector2 textPosition = new Vector2(
                    this.xPositionOnScreen + (this.width / 2) - (textSize.X / 2),
                    this.yPositionOnScreen + (this.height / 2) - (textSize.Y / 2) - 32
                );

                b.DrawString(Game1.dialogueFont, this.statusText, textPosition, Game1.textColor);
            }

            if (this.canModifyDate && this.btnPostpone != null && this.btnAdvance != null)
            {
                this.btnPostpone.draw(b);
                this.btnAdvance.draw(b);

                DrawButtonLabel(b, this.btnAdvance, "Adiantar");
                DrawButtonLabel(b, this.btnPostpone, "Adiar");

                if (this.btnPostpone.containsPoint(Game1.getMouseX(), Game1.getMouseY()))
                    IClickableMenu.drawHoverText(b, "+1 Dia", Game1.smallFont);

                if (this.btnAdvance.containsPoint(Game1.getMouseX(), Game1.getMouseY()))
                    IClickableMenu.drawHoverText(b, "-1 Dia", Game1.smallFont);
            }
            else if (!this.canModifyDate)
            {
                string noPermText = "Apenas o Host ou os Noivos podem alterar a data.";
                Vector2 noPermSize = Game1.smallFont.MeasureString(noPermText);
                Vector2 noPermPos = new Vector2(
                    this.xPositionOnScreen + (this.width / 2) - (noPermSize.X / 2),
                    this.yPositionOnScreen + (this.height / 2) + 64
                );
                b.DrawString(Game1.smallFont, noPermText, noPermPos, Color.Gray);
            }

            this.btnClose.draw(b);
            this.drawMouse(b);
        }

        private void DrawButtonLabel(SpriteBatch b, ClickableTextureComponent btn, string label)
        {
            Vector2 labelSize = Game1.smallFont.MeasureString(label);
            Vector2 labelPos = new Vector2(
                btn.bounds.X + (btn.bounds.Width / 2) - (labelSize.X / 2),
                btn.bounds.Y - labelSize.Y - 8
            );
            b.DrawString(Game1.smallFont, label, labelPos, Game1.textColor);
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            if (this.canModifyDate)
            {
                this.btnPostpone?.tryHover(x, y);
                this.btnAdvance?.tryHover(x, y);
            }
            this.btnClose.tryHover(x, y);
        }
    }
}