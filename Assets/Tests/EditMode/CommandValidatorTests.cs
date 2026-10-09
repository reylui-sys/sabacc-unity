using NUnit.Framework;
using static Sabacc.Core.Tests.TestCards;

namespace Sabacc.Core.Tests
{
    /// <summary>
    /// Especificación de qué acciones son legales. El Master usa este mismo
    /// validador para rechazar comandos, así que estos tests describen
    /// exactamente lo que el servidor de la partida acepta.
    /// </summary>
    [TestFixture]
    public class CommandValidatorTests
    {
        private static readonly PlayerId Ana = new PlayerId(1);
        private static readonly PlayerId Bea = new PlayerId(3);

        private RulesConfig _rules;
        private GameState _state;

        [SetUp]
        public void SetUp()
        {
            _rules = new RulesConfig();
            _state = Game(100, 1, 3);           // Ana (asiento 0) y Bea (asiento 1)
            _state.CurrentPlayerIndex = 0;      // turno de Ana
            GiveHand(_state, 0, Coins(5), Coins(6), Coins(7));
            GiveHand(_state, 1, Coins(2), Coins(3));
        }

        private string Validate(CommandType type, PlayerId who, int arg = 0)
            => CommandValidator.Validate(_state, _rules, new GameCommand(type, who, arg));

        private static void Valid(string result) => Assert.IsNull(result, result);
        private static void Invalid(string result) => Assert.IsNotNull(result, "Se esperaba un rechazo");

        // ===== COMUNES =====

        [Test]
        public void Rechaza_SiNoEsTuTurno()
        {
            _state.CurrentPhase = GamePhase.Drawing;
            Invalid(Validate(CommandType.Draw, Bea));
        }

        [Test]
        public void Rechaza_JugadorQueNoEstaEnLaPartida()
        {
            _state.CurrentPhase = GamePhase.Drawing;
            Invalid(Validate(CommandType.Draw, new PlayerId(2)));
        }

        [Test]
        public void Rechaza_JugadorRetirado()
        {
            _state.CurrentPhase = GamePhase.FirstBetting;
            _state.Players[0].Fold();
            Invalid(Validate(CommandType.Check, Ana));
        }

        [Test]
        public void Rechaza_TipoDeComandoDesconocido()
        {
            _state.CurrentPhase = GamePhase.Drawing;
            Invalid(Validate((CommandType)200, Ana));
        }

        // ===== FASE DE ROBO =====

        [Test]
        public void Robar_PermitidoEnFaseDeRobo()
        {
            _state.CurrentPhase = GamePhase.Drawing;
            Valid(Validate(CommandType.Draw, Ana));
            Valid(Validate(CommandType.Stand, Ana));
        }

        [Test]
        public void Robar_RechazadoFueraDeLaFaseDeRobo()
        {
            _state.CurrentPhase = GamePhase.FirstBetting;
            Invalid(Validate(CommandType.Draw, Ana));
            Invalid(Validate(CommandType.Stand, Ana));
        }

        [Test]
        public void Robar_RechazadoConLaManoLlena()
        {
            _state.CurrentPhase = GamePhase.Drawing;
            _rules.MaxCardsInHand = 3;
            Invalid(Validate(CommandType.Draw, Ana));
        }

        [Test]
        public void TrasDescartar_SoloSePuedePlantarse()
        {
            _state.CurrentPhase = GamePhase.Drawing;
            _state.Players[0].HasDiscardedThisTurn = true;

            Invalid(Validate(CommandType.Draw, Ana));
            Invalid(Validate(CommandType.Discard, Ana, 0));
            Valid(Validate(CommandType.Stand, Ana));
        }

        [Test]
        public void Descartar_RequiereTresCartas_YUnIndiceValido()
        {
            _state.CurrentPhase = GamePhase.Drawing;
            Valid(Validate(CommandType.Discard, Ana, 2));
            Invalid(Validate(CommandType.Discard, Ana, 3));
            Invalid(Validate(CommandType.Discard, Ana, -1));

            GiveHand(_state, 0, Coins(5), Coins(6));
            Invalid(Validate(CommandType.Discard, Ana, 0));
        }

        [Test]
        public void Descartar_UnaCartaProtegida_Rechazado()
        {
            _state.CurrentPhase = GamePhase.Drawing;
            _state.Players[0].Hand.GetCards()[1].SetProtected(true);
            Invalid(Validate(CommandType.Discard, Ana, 1));
        }

        // ===== CAMPO DE INTERFERENCIA =====

        [Test]
        public void Proteger_DejaAlMenosDosCartasSinProteger()
        {
            _state.CurrentPhase = GamePhase.Drawing;
            Valid(Validate(CommandType.Protect, Ana, 0));   // 3 cartas → quedan 2 libres

            GiveHand(_state, 0, Coins(5), Coins(6));
            Invalid(Validate(CommandType.Protect, Ana, 0)); // 2 cartas → quedaría 1
        }

        [Test]
        public void Proteger_MaximoDosCartas()
        {
            _state.CurrentPhase = GamePhase.Drawing;
            GiveHand(_state, 0, Coins(1), Coins(2), Coins(3), Coins(4), Coins(5));
            _state.Players[0].Hand.GetCards()[0].SetProtected(true);
            _state.Players[0].Hand.GetCards()[1].SetProtected(true);

            Invalid(Validate(CommandType.Protect, Ana, 2));
        }

        [Test]
        public void Desproteger_SoloCartasProtegidas()
        {
            _state.CurrentPhase = GamePhase.SecondBetting;
            Invalid(Validate(CommandType.Unprotect, Ana, 0));

            _state.Players[0].Hand.GetCards()[0].SetProtected(true);
            Valid(Validate(CommandType.Unprotect, Ana, 0));
            Invalid(Validate(CommandType.Protect, Ana, 0));
        }

        [Test]
        public void CampoDeInterferencia_NoDisponibleDuranteElShift()
        {
            _state.CurrentPhase = GamePhase.FirstShift;
            Invalid(Validate(CommandType.Protect, Ana, 0));
        }

        // ===== APUESTAS =====

        [Test]
        public void Pasar_SoloSiNoHayNadaQueIgualar()
        {
            _state.CurrentPhase = GamePhase.FirstBetting;
            Valid(Validate(CommandType.Check, Ana));

            _state.CurrentHighestBet = 20;
            Invalid(Validate(CommandType.Check, Ana));
        }

        [Test]
        public void Subir_NecesitaCreditosParaIgualarMasLaSubida()
        {
            _state.CurrentPhase = GamePhase.FirstBetting;
            _state.CurrentHighestBet = 30;

            Valid(Validate(CommandType.Bet, Ana, 70));   // 30 + 70 = 100 créditos
            Invalid(Validate(CommandType.Bet, Ana, 71));
            Invalid(Validate(CommandType.Bet, Ana, 0));
            Invalid(Validate(CommandType.Bet, Ana, -5));
        }

        [Test]
        public void Subir_NoPermitidoEnCalling()
        {
            _state.CurrentPhase = GamePhase.Calling;
            Invalid(Validate(CommandType.Bet, Ana, 10));
        }

        [Test]
        public void Igualar_SoloSiHayAlgoQueIgualar_YSePuedePagar()
        {
            _state.CurrentPhase = GamePhase.SecondBetting;
            Invalid(Validate(CommandType.Match, Ana));

            _state.CurrentHighestBet = 50;
            Valid(Validate(CommandType.Match, Ana));

            _state.Players[0].Credits = 49;
            Invalid(Validate(CommandType.Match, Ana));
        }

        [TestCase(GamePhase.Calling, true)]
        [TestCase(GamePhase.SecondBetting, true)]
        [TestCase(GamePhase.FirstBetting, false)]
        public void Call_SoloEnCallingYSegundaApuesta(GamePhase phase, bool allowed)
        {
            _state.CurrentPhase = phase;
            string result = Validate(CommandType.Call, Ana);
            Assert.AreEqual(allowed, result == null, result);
        }

        [Test]
        public void Call_RequiereHaberIgualado()
        {
            _state.CurrentPhase = GamePhase.SecondBetting;
            _state.CurrentHighestBet = 10;
            Invalid(Validate(CommandType.Call, Ana));
        }

        [Test]
        public void DobleClic_UnaVezQueHasActuado_SeRechazaLaSegundaAccion()
        {
            _state.CurrentPhase = GamePhase.FirstBetting;
            Valid(Validate(CommandType.Check, Ana));

            _state.Players[0].HasActedThisBettingRound = true; // el Master ya aceptó el primer "Pasar"

            Invalid(Validate(CommandType.Check, Ana));
            Invalid(Validate(CommandType.Bet, Ana, 10));
            Invalid(Validate(CommandType.Fold, Ana));
        }

        [Test]
        public void TrasUnCall_NadieMasPuedeApostar()
        {
            _state.CurrentPhase = GamePhase.SecondBetting;
            _state.CallerIndex = 1;
            Invalid(Validate(CommandType.Check, Ana));
        }

        [Test]
        public void Apostar_FueraDeFaseDeApuestas_Rechazado()
        {
            _state.CurrentPhase = GamePhase.Drawing;
            Invalid(Validate(CommandType.Check, Ana));
            Invalid(Validate(CommandType.Fold, Ana));
        }

        // ===== PENALIZACIÓN POR RETIRARSE =====

        [Test]
        public void PenalizacionPorRetirarse_SoloEnPrimeraApuesta_YNuncaMasDeLoQueTienes()
        {
            Player ana = _state.Players[0];

            _state.CurrentPhase = GamePhase.FirstBetting;
            Assert.AreEqual(10, CommandValidator.FoldPenalty(_state, _rules, ana));

            ana.Credits = 4;
            Assert.AreEqual(4, CommandValidator.FoldPenalty(_state, _rules, ana));

            _state.CurrentPhase = GamePhase.SecondBetting;
            Assert.AreEqual(0, CommandValidator.FoldPenalty(_state, _rules, ana));
        }
    }
}
