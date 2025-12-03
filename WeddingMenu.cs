using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace WeddingDateManager
{
    public class WeddingMenu : IClickableMenu
    {
        private readonly IModHelper Helper;

        // Componentes UI
        private ClickableTextureComponent? btnPostpone;
        private ClickableTextureComponent? btnAdvance;
        private ClickableTextureComponent btnClose;

        // Cache do status e dados do casamento
        private string statusText = "Calculando...";
        private Friendship? targetFriendship = null;
        private Farmer? engagedPlayer = null;
        private string spouseName = "";
        private bool canModifyDate = false;

        public WeddingMenu(IModHelper helper)
            : base(Game1.viewport.Width / 2 - 400, Game1.viewport.Height / 2 - 300, 800, 600, true)
        {
            this.Helper = helper;
            this.IdentifyTarget(); // Acha o casamento e define permissões
            this.SetupComponents();
            this.RecalculateStatus(); // Calcula o texto inicial
        }

        private void IdentifyTarget()
        {
            // 1. Varrer todos os jogadores (ONLINE E OFFLINE) para encontrar quem está noivo
            foreach (Farmer farmer in Game1.getAllFarmers())
            {
                foreach (var key in farmer.friendshipData.Keys)
                {
                    var friendship = farmer.friendshipData[key];
                    if (friendship.Status == FriendshipStatus.Engaged && friendship.WeddingDate != null)
                    {
                        this.targetFriendship = friendship;
                        this.engagedPlayer = farmer;

                        // Tenta identificar se é NPC ou Outro Jogador
                        if (long.TryParse(key, out long spousePlayerID))
                        {
                            // É um jogador!
                            Farmer? spousePlayer = Game1.GetPlayer(spousePlayerID);
                            this.spouseName = spousePlayer?.Name ?? "Desconhecido";
                        }
                        else
                        {
                            // É um NPC!
                            NPC? spouseNPC = Game1.getCharacterFromName(key);
                            this.spouseName = spouseNPC?.displayName ?? key;
                        }

                        goto Found; // Sai dos loops aninhados
                    }
                }
            }

        Found:
            // 2. Define permissões: Host ou o próprio noivo(a)
            if (this.engagedPlayer != null)
            {
                this.canModifyDate = Game1.IsMasterGame || Game1.player == this.engagedPlayer;
            }
            else
            {
                // Se não achou ninguém, ninguém pode mexer (provavelmente bug ou casamento já passou)
                this.canModifyDate = false;
                this.statusText = "Nenhum casamento ativo encontrado.";
            }
        }

        [MemberNotNull(nameof(btnClose))]
        private void SetupComponents()
        {
            int centerX = this.xPositionOnScreen + (this.width / 2);
            int centerY = this.yPositionOnScreen + (this.height / 2);

            // Só cria os botões de ação se tiver permissão
            if (this.canModifyDate)
            {
                // Botão Esquerdo (Adiantar / Sooner) - Seta Esquerda
                // Usando sourceRect (352, 495, 12, 11) que é a seta para esquerda nativa
                this.btnAdvance = new ClickableTextureComponent(
                    new Rectangle(centerX - 96, centerY + 32, 48, 44),
                    Game1.mouseCursors,
                    new Rectangle(352, 495, 12, 11),
                    4f
                )
                { myID = 101, label = "Adiantar" };

                // Botão Direito (Adiar / Later) - Seta Direita
                // Usando sourceRect (365, 495, 12, 11) que é a seta para direita nativa
                this.btnPostpone = new ClickableTextureComponent(
                    new Rectangle(centerX + 48, centerY + 32, 48, 44),
                    Game1.mouseCursors,
                    new Rectangle(365, 495, 12, 11),
                    4f
                )
                { myID = 100, label = "Adiar" };
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
            if (this.targetFriendship == null || this.targetFriendship.WeddingDate == null)
            {
                Game1.showRedMessage("Nenhum casamento encontrado para modificar.");
                return;
            }

            // 1. Pega o total de dias absolutos da data atual do casamento
            int currentTotalDays = this.targetFriendship.WeddingDate.TotalDays;

            // 2. Calcula o novo total
            int newTotalDays = currentTotalDays + daysToAdd;

            // 3. Validação: Não permitir agendar para o passado ou para hoje (TotalDays de hoje)
            if (newTotalDays <= Game1.Date.TotalDays)
            {
                Game1.showRedMessage("Nao e possivel agendar para hoje ou passado!");
                return;
            }

            // 4. Converte TotalDays de volta para Ano/Estação/Dia
            int newYear = 1 + (newTotalDays / 112);
            int remainderYear = newTotalDays % 112;
            int newSeasonIndex = remainderYear / 28;
            int newDay = (remainderYear % 28) + 1;

            // 5. Cria a nova data e atribui
            WorldDate newDate = new WorldDate(newYear, GetSeasonFromIndex(newSeasonIndex), newDay);
            this.targetFriendship.WeddingDate = newDate;

            // 6. Atualiza a UI imediatamente
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

            // Título com Scroll Background
            SpriteText.drawStringWithScrollBackground(b, "Gerenciar Casamento",
                this.xPositionOnScreen + (this.width / 2),
                this.yPositionOnScreen - 20);

            // Subtítulo: Quem está casando?
            if (this.engagedPlayer != null)
            {
                string coupleText = $"Casamento de: {this.engagedPlayer.Name} & {this.spouseName}";
                // Usando fonte de diálogo para maior destaque
                Vector2 coupleSize = Game1.dialogueFont.MeasureString(coupleText);
                Vector2 couplePos = new Vector2(
                    this.xPositionOnScreen + (this.width / 2) - (coupleSize.X / 2),
                    this.yPositionOnScreen + 128 // Descendo mais um pouco
                );
                b.DrawString(Game1.dialogueFont, coupleText, couplePos, Game1.textColor);
            }

            // Status Text
            if (!string.IsNullOrEmpty(this.statusText))
            {
                Vector2 textSize = Game1.dialogueFont.MeasureString(this.statusText);
                Vector2 textPosition = new Vector2(
                    this.xPositionOnScreen + (this.width / 2) - (textSize.X / 2),
                    this.yPositionOnScreen + (this.height / 2) - (textSize.Y / 2) - 32
                );

                b.DrawString(Game1.dialogueFont, this.statusText, textPosition, Game1.textColor);
            }

            // Botões (só desenha se existirem)
            if (this.canModifyDate && this.btnPostpone != null && this.btnAdvance != null)
            {
                // Desenha os botões normalmente (sem flip manual, pois agora usamos os sourceRects corretos)
                this.btnPostpone.draw(b);
                this.btnAdvance.draw(b);

                // Desenha os Labels manualmente acima dos botões
                DrawButtonLabel(b, this.btnAdvance, "Adiantar");
                DrawButtonLabel(b, this.btnPostpone, "Adiar");

                // Tooltips (mantidos por garantia)
                if (this.btnPostpone.containsPoint(Game1.getMouseX(), Game1.getMouseY()))
                    IClickableMenu.drawHoverText(b, "+1 Dia", Game1.smallFont);

                if (this.btnAdvance.containsPoint(Game1.getMouseX(), Game1.getMouseY()))
                    IClickableMenu.drawHoverText(b, "-1 Dia", Game1.smallFont);
            }
            else if (!this.canModifyDate && this.engagedPlayer != null)
            {
                // Mensagem de "Sem Permissão"
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
                btn.bounds.Y - labelSize.Y - 4 // 4 pixels acima do botão
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