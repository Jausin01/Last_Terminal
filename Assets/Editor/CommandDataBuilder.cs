using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Command and voice data: the two lexicons (EN / PT), Helena's voice line bank, assigning the recorded takes,
// and the recording script for the voice actress. Assets are created once and then only completed
// (missing verbs / names / lines are added; edits and assigned clips are kept).
public static class CommandDataBuilder
{
    private const string CommandsFolder = "Assets/Data/Commands";
    private const string VoiceFolder = "Assets/Data/Voice";
    private const string VoiceBankPath = VoiceFolder + "/HelenaVoiceLines.asset";
    public const string VoiceAudioFolder = "Assets/Audio/Voice/Helena";
    private const string ScriptPath = VoiceAudioFolder + "/ROTEIRO_Helena.md";

    private static string LexiconPath(GameLanguage language) =>
        $"{CommandsFolder}/Lexicon_{(language == GameLanguage.Portuguese ? "PT" : "EN")}.asset";

    private static string LanguageFolder(GameLanguage language) =>
        $"{VoiceAudioFolder}/{(language == GameLanguage.Portuguese ? "PT" : "EN")}";

    // creates every folder of the path ("Assets/Audio/Voice/Helena")
    private static void EnsurePath(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            HelenaSetupMenu.EnsureFolder(current, parts[i]);
            current += "/" + parts[i];
        }
    }

    // ------------------------------------------------------------------ lexicons
    internal static CommandLexicon EnsureLexicon(GameLanguage language)
    {
        EnsurePath(CommandsFolder);
        string path = LexiconPath(language);
        var lexicon = AssetDatabase.LoadAssetAtPath<CommandLexicon>(path);
        if (lexicon == null)
        {
            lexicon = ScriptableObject.CreateInstance<CommandLexicon>();
            AssetDatabase.CreateAsset(lexicon, path);
        }
        bool pt = language == GameLanguage.Portuguese;
        lexicon.EditorRemoveNames(ObsoleteNames);
        // "blackjack" / "vinte e um" are a game's name now (game_blackjack), not a way to say "let's play"
        lexicon.EditorRemoveVerbPhrases(CommandVerb.Play, new[] { "play blackjack", "let s play blackjack", "blackjack",
                                                                  "vamos jogar blackjack", "bora jogar blackjack", "vinte e um" });
        // "olha" / "confere" / "check": Examine now (she looks at the thing and comments on it)
        lexicon.EditorRemoveVerbPhrases(CommandVerb.GoTo, new[] { "olha", "confere", "check", "check out" });
        lexicon.EditorMerge(language,
            (pt ? PortugueseVerbs : EnglishVerbs).Select(v => new CommandLexicon.VerbPhrases(v.verb, v.phrases)),
            Names.Concat(ElevatorBuilder.LexiconNames()).Concat(CasinoBuilder.LexiconNames()).Concat(CasinoBuilder.GameNames())
                 .Concat(AsylumBuilder.LexiconNames())
                 .Select(n => new CommandLexicon.NamePhrases(n.key, pt ? n.pt : n.en)),
            (pt ? PortugueseModifiers : EnglishModifiers).Select(m => new CommandLexicon.ModifierPhrases(m.modifier, m.phrases)),
            (pt ? PortugueseNumbers : EnglishNumbers).Select((words, i) => new CommandLexicon.NumberPhrases(i + 1, words))
                .Concat(BetNumbers.Select(b => new CommandLexicon.NumberPhrases(b.value, pt ? b.pt : b.en))),
            pt ? PortuguesePronouns : EnglishPronouns);
        EditorUtility.SetDirty(lexicon);
        return lexicon;
    }

    // names no scene has any more (removed from the lexicons so their phrases don't shadow the new ones)
    private static readonly string[] ObsoleteNames = { "Card Tables", "Slot Floor" };

    private static readonly (CommandVerb verb, string[] phrases)[] EnglishVerbs =
    {
        (CommandVerb.GoTo, new[] { "go", "go to", "go into", "go inside", "walk", "walk to", "head", "head to", "move", "move to",
                                   "run", "run to", "get to", "come to", "enter" }),
        // "look at the bed": she goes up to it and says what she thinks ("check" / "check out" were GoTo before)
        (CommandVerb.Examine, new[] { "look", "look at", "examine", "check", "check out", "inspect", "investigate", "what s that",
                                      "what is that", "take a look", "take a look at", "have a look", "read", "search" }),
        (CommandVerb.Stop, new[] { "stop", "wait", "halt", "freeze", "hold on", "hold it", "stay", "stay there", "don t move" }),
        (CommandVerb.PickUp, new[] { "pick up", "pick", "grab", "take", "get", "collect" }),
        (CommandVerb.Open, new[] { "open", "unlock" }),
        (CommandVerb.Close, new[] { "close", "shut" }),
        (CommandVerb.GoThrough, new[] { "go through", "walk through", "pass through", "get through", "through" }),
        (CommandVerb.Use, new[] { "use" }),
        (CommandVerb.Heal, new[] { "heal", "heal yourself", "heal up", "patch up", "patch yourself up" }),
        (CommandVerb.Draw, new[] { "draw", "equip", "ready", "take out", "pull out", "switch to", "arm yourself", "weapon out",
                                   "get your gun", "get your weapon", "draw your weapon", "take out your gun" }),
        (CommandVerb.Holster, new[] { "holster", "put away", "put it away", "lower", "stow" }),
        (CommandVerb.Attack, new[] { "attack", "stab", "kill", "strike", "swing at", "smash" }),
        (CommandVerb.Shoot, new[] { "shoot", "shoot at", "fire", "fire at", "open fire" }),
        (CommandVerb.Reload, new[] { "reload" }),
        (CommandVerb.Save, new[] { "save", "save the game", "save game" }),
        (CommandVerb.Aim, new[] { "aim", "aim at", "point at", "point your gun at", "lock on", "lock on to", "focus", "focus on",
                                  "switch target", "switch target to", "change target", "change target to" }),
        (CommandVerb.Play, new[] { "play", "let s play", "lets play", "play a game", "let s play a game",
                                   "deal me in", "game time" }),
        (CommandVerb.Bet, new[] { "bet", "i bet", "wager", "bet on", "put down" }),
        (CommandVerb.Deal, new[] { "deal", "deal the cards", "deal them", "start" }),
        (CommandVerb.Hit, new[] { "hit", "hit me", "card", "another card", "one more card", "give me a card", "more" }),
        (CommandVerb.Stand, new[] { "stand", "i stand", "i m good", "im good", "no more", "hold", "that s good", "stick" }),
        (CommandVerb.Double, new[] { "double", "double down", "double it" }),
        (CommandVerb.Split, new[] { "split", "split them", "split it" }),
        (CommandVerb.Surrender, new[] { "surrender", "give up", "i give up", "fold" }),
        (CommandVerb.Insurance, new[] { "insurance", "take insurance", "insure", "insure it" }),
        (CommandVerb.Yes, new[] { "yes", "yeah", "yep", "sure", "ok", "okay", "ready", "let s go", "sure thing", "go ahead" }),
        (CommandVerb.No, new[] { "no", "nope", "nah", "no thanks", "not now", "no insurance" }),
        (CommandVerb.Again, new[] { "again", "one more", "another", "another one", "another round", "one more round", "deal again", "next round" }),
        (CommandVerb.Quit, new[] { "quit", "i m done", "im done", "that s enough", "enough", "stop playing", "let s stop", "i m out",
                                   "cash out", "leave the table", "game over", "enough for now" }),
        (CommandVerb.Status, new[] { "status", "how are you", "are you ok", "are you okay", "you ok", "how do you feel", "are you hurt" }),
        (CommandVerb.TellTag, new[] { "tag", "tags", "your tag", "name tag", "tag number", "tag numbers", "your tag number", "your tag numbers",
                                      "tell me your tag numbers", "tell me your tag number", "tell me your number", "tell me the numbers",
                                      "read your tag", "read the tag", "read me your tag", "look at your tag", "check your tag",
                                      "what does your tag say", "what s on your tag", "what s written on your tag", "what s your number",
                                      "your number", "patient number", "your patient number", "bed number", "your bed number",
                                      "badge", "your badge", "read your badge", "id number", "your id", "inmate number", "your inmate number" }),
        (CommandVerb.Dodge, new[] { "dodge", "dodge it", "evade", "jump back", "look out", "watch out", "get out of the way", "move it" }),
        (CommandVerb.Duck, new[] { "duck", "get down", "crouch", "crouch down", "down", "hit the deck", "keep your head down", "stay down" }),
        (CommandVerb.StandUp, new[] { "get up", "stand up", "on your feet", "up", "get back up" }),
        (CommandVerb.Advance, new[] { "advance", "move forward", "go forward", "forward", "move up", "push forward", "get closer",
                                      "close in", "step forward", "keep going" }),
        (CommandVerb.BackOff, new[] { "back off", "back up", "back away", "step back", "get back", "get away", "get away from it",
                                      "move away", "keep your distance", "keep away", "give it some space" }),
        (CommandVerb.TakeCover, new[] { "take cover", "find cover", "get to cover", "cover", "hide", "get to safety", "get somewhere safe" }),
        (CommandVerb.Flee, new[] { "run away", "run for it", "retreat", "fall back", "get out", "get out of here", "escape", "flee", "leave the room" }),
        (CommandVerb.HoldFire, new[] { "hold fire", "hold your fire", "cease fire", "stop shooting", "stop firing", "don t shoot" }),
        (CommandVerb.FireAtWill, new[] { "fire at will", "shoot at will", "shoot everything", "free fire", "weapons free", "light them up", "keep shooting" }),
        (CommandVerb.ChangeClothes, new[] { "put on", "wear", "change into", "change clothes", "change your clothes", "get changed",
                                            "change outfit", "switch outfit", "dress up", "get dressed" }),
        (CommandVerb.LookAtCamera, new[] { "look at the camera", "look at me", "look here", "look into the camera", "face the camera",
                                           "let me see you", "show yourself", "stand where i can see you", "come where i can see you" }),
        (CommandVerb.CombatStance, new[] { "combat position", "combat stance", "battle stance", "battle position", "fighting stance",
                                           "get in position", "on guard", "en garde", "stay on guard", "keep your distance", "keep distance",
                                           "keep your distance from them", "get ready to fight", "be ready" }),
        (CommandVerb.StandDown, new[] { "relax", "at ease", "stand down", "ease up", "you can relax", "lower your guard" }),
        (CommandVerb.TypeCode, new[] { "type", "type in", "press", "punch in", "key in", "input", "dial", "enter the code",
                                       "type the code", "put in the code", "press the button", "push", "push the button" }),
        (CommandVerb.TurnLeftSide, new[] { "show your left side", "turn your left side", "left side", "turn to your left side",
                                           "left profile", "turn left side" }),
        (CommandVerb.TurnRightSide, new[] { "show your right side", "turn your right side", "right side", "turn to your right side",
                                            "right profile", "turn right side", "turn sideways", "show your side", "turn to the side" }),
        (CommandVerb.TurnBack, new[] { "turn around", "show your back", "turn your back", "face away", "back to the camera",
                                       "turn your back to the camera", "show me your back" }),
        (CommandVerb.TurnFront, new[] { "turn to the front", "face front", "turn back around", "turn to face me", "face forward" }),
        (CommandVerb.Spin, new[] { "spin", "spin it", "spin the reels", "pull", "pull the lever", "pull it", "hit the button", "press spin" }),
        (CommandVerb.GoTo, new[] { "go up to", "go down to", "go back to", "go up", "go down" }),
        (CommandVerb.Use, new[] { "ride", "ride to", "ride up to", "ride down to" }),   // no "elevator" in a verb phrase: the name must stay
        (CommandVerb.Call, new[] { "call", "i call", "calling", "pay", "pay to see" }),
        (CommandVerb.Exchange, new[] { "exchange", "exchange my chips", "exchange the chips", "cash in", "trade in", "trade my chips",
                                       "change my chips", "convert", "convert my chips" }),
        (CommandVerb.Buy, new[] { "buy", "purchase", "buy me", "get me" }),
        (CommandVerb.ClearBets, new[] { "clear the bets", "clear bets", "clear", "take the bets back", "remove the bets", "take back the bets" }),
    };

    // no bare "para" (it is also "to": "vai para o depósito")
    private static readonly (CommandVerb verb, string[] phrases)[] PortugueseVerbs =
    {
        (CommandVerb.GoTo, new[] { "vai", "va", "vai para", "vai pra", "vai pro", "vai ate", "ir", "ir para", "anda", "anda ate", "ande",
                                   "corre", "corra", "entra", "entre", "entra no", "entra na", "segue", "siga", "volta", "volte" }),
        // "olha a cama": ela vai até a coisa e comenta ("olha" / "confere" eram GoTo antes)
        (CommandVerb.Examine, new[] { "olha", "olhe", "olha a", "olha o", "examina", "examine", "verifica", "verifique", "confere", "confira",
                                      "investiga", "investigue", "o que e isso", "o que tem", "da uma olhada", "de uma olhada", "observa",
                                      "observe", "le", "leia", "procura", "procure", "vasculha" }),
        (CommandVerb.Stop, new[] { "pare", "para ai", "espera", "espere", "fica ai", "fique ai", "fica parada", "fique parada", "calma", "nao se mexe" }),
        (CommandVerb.PickUp, new[] { "pega", "pegue", "pegar", "apanha", "apanhe", "coleta", "recolhe", "junta" }),
        (CommandVerb.Open, new[] { "abr*", "destranca", "destranque" }),
        (CommandVerb.Close, new[] { "fech*" }),
        (CommandVerb.GoThrough, new[] { "passa pela", "passe pela", "passa por", "passe por", "atravessa", "atravesse", "entra pela", "entre pela" }),
        (CommandVerb.Use, new[] { "usa", "use", "usar", "utiliza" }),
        (CommandVerb.Heal, new[] { "cur*", "se cura", "se trata", "se cuida" }),
        (CommandVerb.Draw, new[] { "arma", "pega a arma", "pegue a arma", "pega sua arma", "pegue sua arma", "usa a arma", "use a arma", "saca", "saque", "equipa", "equipe", "prepara", "prepare", "empunha", "troca para", "troca pra", "muda para", "muda pra" }),
        (CommandVerb.Holster, new[] { "guarda", "guarde", "guarda a arma", "abaixa a arma", "abaixe a arma" }),
        (CommandVerb.Attack, new[] { "ataca", "ataque", "bate", "bata", "golpeia", "acerta", "mata", "mate", "esfaqueia" }),
        (CommandVerb.Shoot, new[] { "atir*", "dispara", "dispare", "fogo" }),
        (CommandVerb.Reload, new[] { "recarreg*" }),
        (CommandVerb.Save, new[] { "salv*" }),
        (CommandVerb.Aim, new[] { "mira", "mire", "mirar", "aponta", "aponte", "aponta para", "aponta pro", "aponta pra", "foca", "foque",
                                  "troca de alvo", "troca o alvo", "troque de alvo", "muda de alvo", "muda o alvo", "mude de alvo" }),
        (CommandVerb.Play, new[] { "vamos jogar", "bora jogar", "jogar", "joga", "vamos jogar um jogo", "bora um jogo",
                                   "uma partida" }),
        (CommandVerb.Bet, new[] { "aposta", "aposto", "apostar", "aposte", "boto", "coloca" }),
        (CommandVerb.Deal, new[] { "da as cartas", "distribui", "pode dar", "comeca", "pode comecar" }),
        (CommandVerb.Hit, new[] { "carta", "mais uma carta", "pede", "peco", "pedir", "me da uma carta", "manda carta", "mais" }),
        (CommandVerb.Stand, new[] { "fico", "parar", "paro", "chega de carta", "to bom", "ta bom assim", "fico com essas", "fico assim", "segura" }),
        (CommandVerb.Double, new[] { "dobra", "dobro", "dobrar", "dobrar a aposta", "dobra a aposta" }),
        (CommandVerb.Split, new[] { "divide", "dividir", "separa", "separar", "divide as cartas" }),
        (CommandVerb.Surrender, new[] { "desisto", "desistir", "me rendo", "entrego" }),
        (CommandVerb.Insurance, new[] { "seguro", "quero seguro", "faz o seguro", "fazer seguro" }),
        (CommandVerb.Yes, new[] { "sim", "bora", "beleza", "pode ser", "claro", "ok", "manda ver", "pronto", "vai la" }),
        (CommandVerb.No, new[] { "nao", "nao quero", "sem seguro", "agora nao", "nao obrigado", "nem" }),
        (CommandVerb.Again, new[] { "mais uma", "bora mais uma", "de novo", "outra", "outra rodada", "mais uma rodada", "mais uma mao" }),
        (CommandVerb.Quit, new[] { "chega", "chega por agora", "chega por hoje", "parar de jogar", "para de jogar", "vamos parar", "to fora",
                                   "acabou", "encerrar", "sair da mesa", "cansei" }),
        (CommandVerb.Status, new[] { "status", "como voce esta", "como vc esta", "voce esta bem", "vc esta bem", "ta bem", "tudo bem", "como esta", "ta machucada" }),
        (CommandVerb.TellTag, new[] { "plaquinha", "placa", "sua plaquinha", "sua placa", "etiqueta", "sua etiqueta", "cracha", "seu cracha",
                                      "numero da plaquinha", "numeros da plaquinha", "numero da placa", "numeros da placa",
                                      "numero da sua plaquinha", "numeros da sua plaquinha", "qual o seu numero", "qual seu numero",
                                      "seu numero", "me fala seu numero", "me fala os numeros", "fala os numeros", "le os numeros",
                                      "le a plaquinha", "le sua plaquinha", "leia a plaquinha", "leia sua plaquinha", "olha a plaquinha",
                                      "olha sua plaquinha", "o que ta escrito na plaquinha", "o que esta escrito na plaquinha",
                                      "o que ta escrito na sua plaquinha", "o que esta escrito na sua plaquinha", "numero do leito",
                                      "seu leito", "numero de paciente", "seu numero de paciente", "numero de detenta", "tag" }),
        (CommandVerb.Dodge, new[] { "desvia", "desvie", "desviar", "esquiva", "esquive", "esquivar", "cuidado", "pula pra tras", "pula para tras",
                                    "sai da frente", "olha o golpe" }),
        (CommandVerb.Duck, new[] { "abaixa", "abaixe", "se abaixa", "se abaixe", "agacha", "agache", "se agacha", "abaixa a cabeca",
                                   "no chao", "fica abaixada", "fique abaixada" }),
        (CommandVerb.StandUp, new[] { "levanta", "levante", "se levanta", "fica de pe", "fique de pe", "de pe", "pode levantar" }),
        (CommandVerb.Advance, new[] { "avanca", "avance", "avancar", "vai pra frente", "va pra frente", "pra frente", "segue em frente",
                                      "anda pra frente", "chega mais perto", "se aproxima", "aproxima", "continua", "continue", "vai em frente" }),
        (CommandVerb.BackOff, new[] { "recua", "recue", "recuar", "se afasta", "se afaste", "afasta", "afaste", "sai de perto", "sai de perto dele",
                                      "sai de perto dela", "sai de perto disso", "para tras", "vai pra tras", "da um passo pra tras", "mantem distancia",
                                      "mantenha distancia" }),
        (CommandVerb.TakeCover, new[] { "se protege", "se proteja", "protege", "se esconde", "se esconda", "esconde", "procura cobertura",
                                        "cobertura", "vai pra um lugar seguro" }),
        (CommandVerb.Flee, new[] { "foge", "fuja", "fugir", "foge daqui", "corre daqui", "sai daqui", "vamos sair daqui", "retirada",
                                   "bate em retirada", "sai da sala", "da o fora" }),
        (CommandVerb.HoldFire, new[] { "para de atirar", "pare de atirar", "nao atira", "nao atire", "cessar fogo", "segura o fogo", "segura fogo" }),
        (CommandVerb.FireAtWill, new[] { "atira a vontade", "atire a vontade", "fogo a vontade", "manda bala", "pode atirar", "atira em tudo",
                                         "continua atirando", "nao para de atirar" }),
        (CommandVerb.ChangeClothes, new[] { "veste", "vista", "vestir", "se veste", "troca de roupa", "troca a roupa", "troque de roupa",
                                            "muda de roupa", "muda a roupa", "bota o", "bota a", "se troca", "poe o", "poe a" }),
        (CommandVerb.LookAtCamera, new[] { "olha pra camera", "olha para a camera", "olhe para a camera", "olha pra mim", "olhe pra mim",
                                           "olha aqui", "olha para mim", "deixa eu te ver", "aparece", "fica onde eu te veja", "vem pra camera" }),
        (CommandVerb.CombatStance, new[] { "posicao de combate", "posicao de batalha", "em posicao", "em guarda", "fica em guarda",
                                           "fique em guarda", "mantem distancia", "mantenha distancia", "mantem a distancia", "mantenha a distancia",
                                           "fica longe deles", "prepara pra lutar", "se prepara pra lutar" }),
        (CommandVerb.StandDown, new[] { "relaxa", "relaxe", "descansa", "descanse", "pode relaxar", "sai da posicao", "baixa a guarda", "abaixa a guarda" }),
        (CommandVerb.TypeCode, new[] { "digita", "digite", "digitar", "aperta", "aperte", "apertar", "pressiona", "pressione", "tecla",
                                       "coloca o codigo", "coloca a senha", "poe a senha", "poe o codigo", "digita a senha", "digita o codigo",
                                       "aperta o botao", "aperte o botao" }),
        (CommandVerb.TurnLeftSide, new[] { "vira de lado esquerdo", "vira o lado esquerdo", "mostra o lado esquerdo", "lado esquerdo",
                                           "de lado esquerdo", "fica de lado esquerdo", "vira pra esquerda", "vira para a esquerda",
                                           "vire para a esquerda", "vire de lado esquerdo" }),
        (CommandVerb.TurnRightSide, new[] { "vira de lado direito", "vira o lado direito", "mostra o lado direito", "lado direito",
                                            "de lado direito", "fica de lado direito", "vira pra direita", "vira para a direita",
                                            "vire para a direita", "vire de lado direito", "vira de lado", "fica de lado", "vire de lado" }),
        (CommandVerb.TurnBack, new[] { "vira de costas", "fica de costas", "de costas", "mostra as costas", "vira pra tras",
                                       "vire de costas", "vira as costas", "fique de costas" }),
        (CommandVerb.TurnFront, new[] { "vira de frente", "fica de frente", "de frente", "vira pra frente", "vire de frente", "fique de frente" }),
        (CommandVerb.Spin, new[] { "gira", "gire", "girar", "roda", "rode", "rodar", "puxa", "puxe", "puxa a alavanca", "aperta o spin", "joga de novo" }),
        (CommandVerb.GoTo, new[] { "sobe", "suba", "sobe pro", "sobe para", "desce", "desca", "desce pro", "desce para", "volta pro", "volta para" }),
        (CommandVerb.Call, new[] { "pago", "pagar", "paga", "eu pago", "pago pra ver", "vou pagar", "pago para ver" }),
        (CommandVerb.Surrender, new[] { "larga", "largo", "foldo", "fold", "corro" }),
        (CommandVerb.Exchange, new[] { "troca", "troque", "trocar", "troca as fichas", "troca minhas fichas", "troca as minhas fichas",
                                       "converte", "converter", "converte as fichas" }),
        (CommandVerb.Buy, new[] { "compra", "compre", "comprar", "compra pra mim", "quero comprar" }),
        (CommandVerb.ClearBets, new[] { "tira as apostas", "limpa as apostas", "limpa", "cancela as apostas", "tira tudo da mesa" }),
    };

    // which one ("the other dummy", "o boneco da esquerda"); left / right as seen on the camera
    private static readonly (CommandModifier modifier, string[] phrases)[] EnglishModifiers =
    {
        (CommandModifier.Other, new[] { "other", "other one", "another", "another one", "next", "next one" }),
        (CommandModifier.Left, new[] { "left", "on the left", "left one" }),
        (CommandModifier.Right, new[] { "right", "on the right", "right one" }),
        (CommandModifier.Nearest, new[] { "closest", "nearest", "closer", "near", "closest one", "nearest one" }),
        (CommandModifier.Farthest, new[] { "farthest", "furthest", "far", "far one", "farther", "farthest one", "furthest one" }),
    };

    private static readonly (CommandModifier modifier, string[] phrases)[] PortugueseModifiers =
    {
        (CommandModifier.Other, new[] { "outro", "outra", "o outro", "a outra", "proximo", "proxima" }),
        (CommandModifier.Left, new[] { "esquerda", "da esquerda", "a esquerda" }),
        (CommandModifier.Right, new[] { "direita", "da direita", "a direita" }),
        (CommandModifier.Nearest, new[] { "mais perto", "mais proximo", "mais proxima", "perto" }),
        (CommandModifier.Farthest, new[] { "mais longe", "mais distante", "longe", "do fundo" }),
    };

    // spoken numbers 1..10 (index + 1); digits always work
    private static readonly string[][] EnglishNumbers =
    {
        new[] { "one", "first" }, new[] { "two", "second" }, new[] { "three", "third" }, new[] { "four", "fourth" },
        new[] { "five", "fifth" }, new[] { "six", "sixth" }, new[] { "seven", "seventh" }, new[] { "eight", "eighth" },
        new[] { "nine", "ninth" }, new[] { "ten", "tenth" },
    };

    // bet amounts (value, words)
    private static readonly (int value, string[] en, string[] pt)[] BetNumbers =
    {
        // roulette numbers (11..36; 15, 20, 25 and 30 are below)
        (11, new[] { "eleven" }, new[] { "onze" }), (12, new[] { "twelve" }, new[] { "doze" }), (13, new[] { "thirteen" }, new[] { "treze" }),
        (14, new[] { "fourteen" }, new[] { "quatorze", "catorze" }), (16, new[] { "sixteen" }, new[] { "dezesseis" }),
        (17, new[] { "seventeen" }, new[] { "dezessete" }), (18, new[] { "eighteen" }, new[] { "dezoito" }), (19, new[] { "nineteen" }, new[] { "dezenove" }),
        (21, new[] { "twenty one" }, new[] { "vinte e um", "vinte e uma" }), (22, new[] { "twenty two" }, new[] { "vinte e dois" }),
        (23, new[] { "twenty three" }, new[] { "vinte e tres" }), (24, new[] { "twenty four" }, new[] { "vinte e quatro" }),
        (26, new[] { "twenty six" }, new[] { "vinte e seis" }), (27, new[] { "twenty seven" }, new[] { "vinte e sete" }),
        (28, new[] { "twenty eight" }, new[] { "vinte e oito" }), (29, new[] { "twenty nine" }, new[] { "vinte e nove" }),
        (31, new[] { "thirty one" }, new[] { "trinta e um" }), (32, new[] { "thirty two" }, new[] { "trinta e dois" }),
        (33, new[] { "thirty three" }, new[] { "trinta e tres" }), (34, new[] { "thirty four" }, new[] { "trinta e quatro" }),
        (35, new[] { "thirty five" }, new[] { "trinta e cinco" }), (36, new[] { "thirty six" }, new[] { "trinta e seis" }),
        (15, new[] { "fifteen" }, new[] { "quinze" }),
        (20, new[] { "twenty" }, new[] { "vinte" }),
        (25, new[] { "twenty five" }, new[] { "vinte e cinco" }),
        (30, new[] { "thirty" }, new[] { "trinta" }),
        (40, new[] { "forty" }, new[] { "quarenta" }),
        (50, new[] { "fifty" }, new[] { "cinquenta" }),
        (60, new[] { "sixty" }, new[] { "sessenta" }),
        (75, new[] { "seventy five" }, new[] { "setenta e cinco" }),
        (100, new[] { "hundred", "a hundred", "one hundred" }, new[] { "cem", "cento" }),
        (150, new[] { "one fifty", "a hundred fifty", "one hundred fifty" }, new[] { "cento e cinquenta" }),
        (200, new[] { "two hundred" }, new[] { "duzentos" }),
        (250, new[] { "two fifty", "two hundred fifty" }, new[] { "duzentos e cinquenta" }),
        (300, new[] { "three hundred" }, new[] { "trezentos" }),
        (400, new[] { "four hundred" }, new[] { "quatrocentos" }),
        (500, new[] { "five hundred" }, new[] { "quinhentos" }),
        (1000, new[] { "a thousand", "one thousand", "thousand" }, new[] { "mil" }),
    };

    private static readonly string[][] PortugueseNumbers =
    {
        new[] { "um", "uma", "primeiro", "primeira" }, new[] { "dois", "duas", "segundo", "segunda" }, new[] { "tres", "terceiro", "terceira" },
        new[] { "quatro", "quarto", "quarta" }, new[] { "cinco", "quinto", "quinta" }, new[] { "seis", "sexto", "sexta" },
        new[] { "sete", "setimo", "setima" }, new[] { "oito", "oitavo", "oitava" }, new[] { "nove", "nono", "nona" }, new[] { "dez", "decimo", "decima" },
    };

    private static readonly string[] EnglishPronouns = { "it", "that", "this", "there", "them", "those" };
    private static readonly string[] PortuguesePronouns = { "isso", "isto", "aquilo", "ali", "la", "ele", "ela", "nele", "nela" };

    // key: Room / Door display name, item id, weapon prefab object name, CommandTargets keys
    private static readonly (string key, string[] en, string[] pt)[] Names =
    {
        ("Lobby", new[] { "lobby", "reception", "entrance", "hall" }, new[] { "lobby", "recepcao", "entrada", "saguao", "hall" }),
        ("Office", new[] { "office" }, new[] { "escritorio" }),
        ("Training Room", new[] { "training room", "training", "gym" }, new[] { "sala de treino", "sala de treinamento", "treino", "treinamento" }),
        ("Corridor", new[] { "corridor", "hallway" }, new[] { "corredor" }),
        ("Storage", new[] { "storage", "storage room", "storeroom", "supply room" }, new[] { "deposito", "almoxarifado", "estoque", "despensa" }),
        (CommandTargets.DoorKey, new[] { "door" }, new[] { "porta" }),
        (CommandTargets.ComputerKey, new[] { "computer", "terminal", "save point", "pc" }, new[] { "computador", "terminal", "pc" }),
        (CommandTargets.DummyKey, new[] { "dummy", "mannequin", "training dummy" }, new[] { "boneco", "manequim" }),
        (CommandTargets.AllInKey, new[] { "all in", "everything", "all of it", "all my chips" }, new[] { "tudo", "all in", "tudo que eu tenho", "todas as fichas" }),
        (CommandTargets.MinBetKey, new[] { "minimum", "the minimum", "min" }, new[] { "minimo", "o minimo", "aposta minima" }),
        (CommandTargets.MaxBetKey, new[] { "maximum", "the maximum", "max" }, new[] { "maximo", "o maximo", "aposta maxima" }),
        (CommandTargets.SameBetKey, new[] { "same", "same bet", "same again", "the same" }, new[] { "mesma", "mesma aposta", "o mesmo", "a mesma", "igual" }),
        ("outfit_pajama", new[] { "pajamas", "pajama", "pyjamas", "pjs", "sleepwear", "pajama outfit" },
                          new[] { "pijama", "pijaminha", "roupa de dormir" }),
        (OutfitBuilder.PatientOutfitKey, new[] { "patient clothes", "patient outfit", "scrubs", "hospital clothes", "patient gown" },
                                         new[] { "roupa de paciente", "roupa de hospital", "roupa do hospicio", "pijama de hospital" }),
        ("outfit_prison", new[] { "prison clothes", "prison uniform", "prison outfit", "inmate uniform", "stripes", "striped clothes" },
                          new[] { "roupa de prisao", "uniforme de prisao", "roupa de presidiaria", "uniforme listrado", "roupa listrada", "listrada" }),
        (CommandTargets.CenterKey, new[] { "center", "centre", "middle", "center of the room", "middle of the room", "room center" },
                                   new[] { "centro", "meio", "centro da sala", "meio da sala" }),
        ("outfit_bunny", new[] { "bunny suit", "bunny outfit", "bunny girl", "bunny costume", "bunny", "bunny ears" },
                         new[] { "coelhinha", "roupa de coelhinha", "fantasia de coelhinha", "bunny", "coelho", "orelhinhas" }),
        (PartnerOutfit.DefaultKey, new[] { "your clothes", "clothes", "normal clothes", "usual clothes", "regular clothes", "your jacket", "your outfit", "normal outfit" },
                                   new[] { "roupa normal", "sua roupa", "roupa de sempre", "sua jaqueta", "roupa de antes" }),
        ("Padded Cell", new[] { "cell", "padded cell", "my cell", "the cell" }, new[] { "cela", "cela acolchoada", "solitaria", "minha cela" }),
        (CommandTargets.KeypadKey, new[] { "keypad", "key pad", "code panel", "number pad", "code", "password", "keys", "panel" },
                                   new[] { "teclado", "teclado numerico", "painel", "keypad", "senha", "codigo", "teclas" }),
        ("zombie", new[] { "zombie", "zombies", "walker", "runner", "infected", "ghoul", "dead guy" },
                   new[] { "zumbi", "zumbis", "zombie", "morto vivo", "infectado" }),
        ("killer", new[] { "killer", "burnt man", "burned man", "burnt guy", "murderer", "butcher", "maniac" },
                   new[] { "assassino", "queimado", "homem queimado", "matador", "acougueiro", "maniaco" }),
        (CommandTargets.EnemyKey, new[] { "enemy", "enemies", "target", "monster", "creature", "thing", "number" },
                                  new[] { "inimigo", "inimigos", "alvo", "monstro", "criatura", "numero" }),
        ("item_ammo_handgun", new[] { "handgun ammo", "pistol ammo", "bullets", "ammo" }, new[] { "municao de pistola", "balas de pistola", "balas", "municao" }),
        ("item_ammo_shotgun", new[] { "shotgun shells", "shotgun ammo", "shells", "ammo" }, new[] { "cartuchos", "cartucho", "municao de escopeta", "municao de espingarda", "municao" }),
        ("item_ammo_magnum", new[] { "magnum ammo", "magnum rounds", "magnum bullets", "ammo" }, new[] { "municao magnum", "balas magnum", "municao de magnum", "municao" }),
        ("item_medkit", new[] { "medkit", "med kit", "first aid", "first aid kit", "health kit" }, new[] { "kit medico", "kit de primeiros socorros", "primeiros socorros", "medkit", "remedio" }),
        ("item_keycard", new[] { "keycard", "key card", "card", "key" }, new[] { "cartao", "cartao de acesso", "chave" }),
        ("Weapon_Pistol1911", new[] { "pistol", "handgun", "colt", "1911", "gun" }, new[] { "pistola", "colt", "1911" }),
        ("Weapon_DesertEagle", new[] { "desert eagle", "deagle", "magnum", "eagle" }, new[] { "desert eagle", "deagle", "magnum" }),
        ("Weapon_ShotgunMossberg", new[] { "shotgun", "mossberg" }, new[] { "escopeta", "espingarda", "shotgun", "doze" }),
        ("Weapon_KitchenKnife", new[] { "knife", "kitchen knife" }, new[] { "faca" }),
        ("Weapon_Cleaver", new[] { "cleaver", "meat cleaver" }, new[] { "cutelo" }),
        ("Weapon_Machete", new[] { "machete" }, new[] { "facao", "machete" }),
        ("Weapon_Hammer", new[] { "hammer" }, new[] { "martelo" }),
        ("Weapon_Bat", new[] { "bat", "baseball bat" }, new[] { "taco", "taco de beisebol" }),
        ("Weapon_NailBat", new[] { "nail bat", "spiked bat", "bat with nails" }, new[] { "taco com pregos", "taco de pregos" }),
        ("Weapon_Axe", new[] { "axe" }, new[] { "machado" }),
    };

    // ------------------------------------------------------------------ voice lines
    internal static VoiceLineBank EnsureVoiceBank()
    {
        EnsurePath(VoiceFolder);
        var bank = AssetDatabase.LoadAssetAtPath<VoiceLineBank>(VoiceBankPath);
        if (bank == null)
        {
            bank = ScriptableObject.CreateInstance<VoiceLineBank>();
            AssetDatabase.CreateAsset(bank, VoiceBankPath);
        }
        bank.EditorMerge(Lines.Select(l => new VoiceLineBank.Entry(l.line, l.context, l.en, l.pt)));
        EditorUtility.SetDirty(bank);
        return bank;
    }

    private static readonly (PartnerLine line, string context, string en, string pt)[] Lines =
    {
        (PartnerLine.Acknowledge, "Recebeu uma ordem e vai fazer (pegar, abrir porta, atacar).", "Okay.", "Tá bom."),
        (PartnerLine.OnMyWay, "Começa a andar até onde mandaram.", "On my way.", "Tô indo."),
        (PartnerLine.NotUnderstood, "Não entendeu o que o operador falou.", "What? I didn't get that.", "Hã? Não entendi."),
        (PartnerLine.Busy, "Está no meio de outra coisa (sentada, abrindo porta, pegando item).", "Not now, hold on.", "Agora não, espera."),
        (PartnerLine.CantGetThere, "Não tem caminho até o lugar.", "I can't get there from here.", "Não consigo chegar lá daqui."),
        (PartnerLine.NothingThere, "Procurou o que pediram e não achou.", "I don't see anything like that.", "Não tô vendo nada disso."),
        (PartnerLine.GotIt, "Pegou o item.", "Got it.", "Peguei."),
        (PartnerLine.TookSome, "Só coube parte do item na bolsa.", "I can't carry all of it.", "Não coube tudo."),
        (PartnerLine.BagFull, "Inventário cheio.", "I can't carry any more.", "Não tenho mais espaço."),
        (PartnerLine.CantReach, "O item está fora de alcance.", "I can't reach it.", "Não alcanço."),
        (PartnerLine.DoorLocked, "Forçou a maçaneta e a porta está trancada.", "It's locked.", "Tá trancada."),
        (PartnerLine.DoorNeedsOperator, "Porta de segurança: pede para o operador abrir pelas câmeras.", "It won't open. Can you open it from there?", "Não abre. Você consegue abrir daí?"),
        (PartnerLine.DoorUnlocked, "Destrancou a porta com a chave/cartão.", "The key worked.", "A chave funcionou."),
        (PartnerLine.DoorAlreadyOpen, "Mandaram abrir uma porta já aberta.", "It's already open.", "Já tá aberta."),
        (PartnerLine.DoorAlreadyClosed, "Mandaram fechar uma porta já fechada.", "It's already closed.", "Já tá fechada."),
        (PartnerLine.Healed, "Usou o kit médico. Alívio.", "That's better.", "Bem melhor."),
        (PartnerLine.NotHurt, "Mandaram se curar mas ela está bem.", "I'm not hurt.", "Eu tô bem, não preciso."),
        (PartnerLine.NoHealing, "Mandaram se curar e não tem kit.", "I don't have anything to heal with.", "Não tenho nada pra me curar."),
        (PartnerLine.WeaponReady, "Sacou a arma. Tensa, focada.", "Ready.", "Pronta."),
        (PartnerLine.WeaponAway, "Guardou a arma.", "Putting it away.", "Guardando."),
        (PartnerLine.DontHaveThat, "Pediram uma arma ou item que ela não tem.", "I don't have that.", "Eu não tenho isso."),
        (PartnerLine.NoTarget, "Mandaram atirar/atacar e não tem alvo.", "There's nothing to shoot.", "Não tem nada pra atirar."),
        (PartnerLine.OutOfAmmo, "Sem munição. Urgente, nervosa.", "I'm out of ammo!", "Tô sem munição!"),
        (PartnerLine.Reloading, "Começa a recarregar.", "Reloading.", "Recarregando."),
        (PartnerLine.AlreadyLoaded, "Mandaram recarregar e a arma está cheia.", "It's already loaded.", "Já tá carregada."),
        (PartnerLine.NoGun, "Mandaram atirar/recarregar sem arma de fogo na mão.", "I'm not holding a gun.", "Eu não tô com arma na mão."),
        (PartnerLine.Saving, "Vai até o computador salvar.", "I'll save here.", "Vou salvar aqui."),
        (PartnerLine.NoSavePoint, "Não tem computador por perto.", "There's no computer around here.", "Não tem nenhum computador por aqui."),
        (PartnerLine.StatusFine, "Perguntaram como ela está: inteira.", "I'm fine.", "Tô bem."),
        (PartnerLine.StatusHurt, "Perguntaram como ela está: machucada, aguentando.", "I'm hurt, but I can keep going.", "Tô machucada, mas dá pra continuar."),
        (PartnerLine.StatusBadlyHurt, "Perguntaram como ela está: muito ferida, voz fraca.", "I'm hurt badly...", "Tô muito machucada..."),
        (PartnerLine.Stopping, "Mandaram parar.", "Stopping.", "Parei."),
        (PartnerLine.CombatStart, "Viu inimigos, começa o combate. Susto, alerta.", "Something's here!", "Tem alguma coisa aqui!"),
        (PartnerLine.TargetDown, "Derrubou o alvo que estava atacando. Ofegante.", "It's down.", "Derrubei."),
        (PartnerLine.Aiming, "Mirou no alvo que o operador escolheu.", "I've got it.", "Tô na mira."),
        (PartnerLine.NoSuchTarget, "O operador falou um número de inimigo que não existe.", "Which one? I don't see that number.", "Qual? Não vejo esse número."),
        (PartnerLine.BjAccept, "O operador chamou pra jogar. Animada, topa na hora.", "A game? Sure, why not.", "Um joguinho? Bora, por que não."),
        (PartnerLine.BjNoTable, "Chamou pra jogar mas não tem mesa por perto.", "There's no table around here.", "Não tem mesa nenhuma por aqui."),
        (PartnerLine.BjNoGame, "Falou de aposta sem estar jogando.", "We're not playing anything.", "A gente nem tá jogando."),
        (PartnerLine.BjPlaceBet, "Sentada na mesa, pede a aposta. Tom de dealer.", "Place your bet.", "Faça sua aposta."),
        (PartnerLine.BjBetLimits, "Aposta fora do limite da mesa.", "Table limits, sorry.", "Limite da mesa, foi mal."),
        (PartnerLine.BjNotEnough, "O operador não tem fichas pra isso.", "You don't have that much.", "Você não tem tudo isso."),
        (PartnerLine.BjYourTurn, "Cartas dadas, vez do operador.", "Hit or stand?", "Carta ou para?"),
        (PartnerLine.BjInsurance, "Ela mostra um ás: oferece seguro.", "Insurance?", "Quer seguro?"),
        (PartnerLine.BjPeek, "Vai espiar a carta virada. Suspense.", "Let me check...", "Deixa eu ver..."),
        (PartnerLine.BjDealerBlackjack, "Ela tem blackjack. Fingindo pena.", "Blackjack. Sorry!", "Blackjack. Foi mal!"),
        (PartnerLine.BjPlayerBlackjack, "O operador tirou blackjack. Surpresa.", "Blackjack! Lucky you.", "Blackjack! Que sorte, hein."),
        (PartnerLine.BjPlayerBust, "O operador estourou. Provocando.", "Bust. Too greedy.", "Estourou. Ganancioso, hein?"),
        (PartnerLine.BjDealerBust, "Ela estourou. Frustrada.", "I bust... ugh.", "Estourei... droga."),
        (PartnerLine.BjPlayerWins, "O operador ganhou a mão.", "You win.", "Você ganhou."),
        (PartnerLine.BjHouseWins, "Ela ganhou a mão. Convencida.", "House wins.", "A casa ganha."),
        (PartnerLine.BjPush, "Empate.", "Push.", "Empate."),
        (PartnerLine.BjCountingCards, "O operador ganhou 3 seguidas. Desconfiada, brincando.", "Are you counting cards?", "Você tá contando cartas, né?"),
        (PartnerLine.BjAnotherRound, "Fim da mão: pergunta se joga mais uma.", "Another round?", "Mais uma?"),
        (PartnerLine.BjFinishHand, "Quis parar ou sair no meio da mão.", "Let's finish this hand first.", "Vamos terminar essa mão primeiro."),
        (PartnerLine.BjGameOver, "Pararam de jogar, ela guarda o baralho e levanta.", "That was fun. Back to work.", "Foi divertido. De volta ao trabalho."),
        (PartnerLine.BjBroke, "O operador ficou sem fichas: ela dá umas de presente.", "You're broke. Here, on the house.", "Você quebrou. Toma, essa é por conta da casa."),
        (PartnerLine.BjIdle, "O operador está demorando pra decidir.", "Take your time... I'm not going anywhere.", "Pode pensar... eu não vou a lugar nenhum."),
        (PartnerLine.BjCantDouble, "Pediu pra dobrar quando não dá.", "You can't double now.", "Não dá pra dobrar agora."),
        (PartnerLine.BjCantSplit, "Pediu pra dividir uma mão que não é par.", "You can't split that.", "Não dá pra dividir essa."),
        (PartnerLine.BjCantSurrender, "Pediu pra desistir depois da hora.", "Too late to surrender.", "Tarde demais pra desistir."),
        (PartnerLine.BjShuffle, "Acabou o baralho, ela embaralha um novo.", "New shoe. Let me shuffle.", "Baralho novo. Deixa eu embaralhar."),
        (PartnerLine.NotNow, "No meio do combate pediram outra coisa (abrir porta, jogar...). Ríspida, ofegante.", "Not now, I'm fighting!", "Agora não, eu tô lutando!"),
        (PartnerLine.Dodging, "Pula pra trás desviando de um golpe. Esforço.", "Whoa!", "Opa!"),
        (PartnerLine.Ducking, "Se abaixa rápido.", "Getting down!", "Abaixando!"),
        (PartnerLine.GettingUp, "Levanta depois de estar abaixada.", "Getting up.", "Levantando."),
        (PartnerLine.BackingOff, "Se afasta do inimigo sem tirar os olhos dele.", "Backing off!", "Tô recuando!"),
        (PartnerLine.Retreating, "Foge do combate correndo pra porta. Desesperada.", "I'm getting out of here!", "Vou sair daqui!"),
        (PartnerLine.TakingCover, "Corre pro canto mais longe dos inimigos.", "Taking cover!", "Vou me proteger!"),
        (PartnerLine.HoldingFire, "Para de atirar mas continua mirando.", "Holding fire.", "Segurando o fogo."),
        (PartnerLine.FireAtWill, "Liberada pra atirar em tudo. Determinada.", "Firing at will!", "Fogo à vontade!"),
        (PartnerLine.NoThreat, "Mandaram recuar/se proteger/fugir sem inimigo por perto.", "From what? There's nothing here.", "Do quê? Não tem nada aqui."),
        (PartnerLine.NoWayOut, "Quer fugir/se proteger mas não tem pra onde ir. Medo.", "There's nowhere to go!", "Não tem pra onde ir!"),
        (PartnerLine.OutfitChanged, "Trocou de roupa na frente da câmera. Brincalhona, um pouco envergonhada.", "Don't peek! ...Okay, done.", "Não espia! ...Pronto."),
        (PartnerLine.OutfitSame, "Pediram a roupa que ela já está usando.", "I'm already wearing it.", "Eu já tô com ela."),
        (PartnerLine.TooFar, "Mandaram ela pra um lugar longe (não é a sala do lado). Pede pra guiar por partes.", "That's too far. Guide me one room at a time.", "É longe demais. Me guia uma sala de cada vez."),
        (PartnerLine.LookWhat, "Parou na frente da câmera e olhou pra ela, esperando. Meio desconfiada.", "What?", "Que foi?"),
        (PartnerLine.LookWaiting, "Ficou um tempão olhando pra câmera e nada aconteceu.", "...Are you just going to stare?", "...Vai ficar só me olhando?"),
        (PartnerLine.NoCamera, "Mandaram olhar pra câmera mas a câmera que o operador vê não é da sala dela.", "Which camera? I can't see it from here.", "Qual câmera? Não tô vendo nenhuma daqui."),
        (PartnerLine.Hurt, "Levou um golpe. Grito curto de dor (várias versões ajudam: _01, _02, _03).", "Agh!", "Ai!"),
        (PartnerLine.HurtBad, "Ferida feio, mancando. Fraca, assustada.", "I'm hurt bad... I need to patch myself up.", "Tô muito machucada... preciso me cuidar."),
        (PartnerLine.Dying, "Golpe final, ela cai. Último suspiro.", "No...", "Não..."),
        (PartnerLine.StanceRanged, "Entra em posição de combate com a arma de fogo. Focada.", "In position. I'll keep my distance.", "Em posição. Vou manter distância."),
        (PartnerLine.StanceMelee, "Em guarda com a arma branca. Tensa, esperando a ordem.", "On guard. Tell me when.", "Em guarda. É só mandar."),
        (PartnerLine.StandingDown, "Sai da posição de combate. Solta o ar.", "Okay... relaxing.", "Tá... relaxando."),
        (PartnerLine.Cornered, "Não tem mais pra onde recuar. Assustada.", "I'm cornered!", "Tô encurralada!"),
        (PartnerLine.KeypadOpen, "Digitou a senha certa e a porta abriu. Alívio.", "It worked... it's open!", "Deu certo... abriu!"),
        (PartnerLine.KeypadWrong, "O teclado apitou erro. Frustrada.", "Wrong code.", "Senha errada."),
        (PartnerLine.NoKeypad, "Mandaram digitar mas não tem teclado por perto.", "There's no keypad here.", "Não tem teclado aqui."),
        (PartnerLine.KeypadWhich, "Está no teclado esperando os números.", "Okay. What's the code?", "Tá. Qual é a senha?"),
        (PartnerLine.TurnSide, "Vira de lado pra câmera, meio sem graça.", "Like this?", "Assim?"),
        (PartnerLine.TurnBack, "Vira de costas pra câmera. Desconfiada.", "My back? ...Okay.", "De costas? ...Tá."),
        (PartnerLine.ElevatorGoing, "Vai pegar o elevador que o operador mandou.", "Taking the elevator.", "Vou pegar o elevador."),
        (PartnerLine.ElevatorNeedKey, "O elevador precisa de uma chave colorida que ela não tem (a cor está na placa).", "It needs a key. The color on the sign.", "Precisa de uma chave. Da cor da placa."),
        (PartnerLine.ElevatorOutOfService, "Esse elevador não leva a lugar nenhum por enquanto.", "This one's out of service.", "Esse aqui tá fora de serviço."),
        (PartnerLine.NoElevator, "Mandaram pegar um elevador que não existe aqui.", "There's no elevator like that here.", "Não tem elevador assim aqui."),
        (PartnerLine.SlotReady, "Chegou no caça-níquel. Animada, supersticiosa.", "Okay... let's see if I'm lucky.", "Tá... vamos ver se eu tô com sorte."),
        (PartnerLine.SlotWin, "Ganhou algo no caça-níquel. Contente.", "We won something!", "Ganhamos alguma coisa!"),
        (PartnerLine.SlotJackpot, "Prêmio grande no caça-níquel. Eufórica.", "Jackpot! Look at that!", "Jackpot! Olha isso!"),
        (PartnerLine.SlotNoChips, "Acabaram as fichas pra apostar.", "We're out of chips.", "Acabaram as fichas."),
        (PartnerLine.ElevatorArrived, "Saiu do elevador num lugar novo. Olhando em volta.", "Here we are.", "Chegamos."),
        (PartnerLine.RouletteBets, "Chegou na roleta como crupiê. Tom de cassino, divertida.", "Place your bets.", "Façam suas apostas."),
        (PartnerLine.RouletteNoMoreBets, "Girou a roleta. Clássico do crupiê.", "No more bets!", "Apostas encerradas!"),
        (PartnerLine.RouletteWin, "A bola caiu num número que deu lucro. Animada.", "We won!", "Ganhamos!"),
        (PartnerLine.RouletteLose, "A bola caiu e nada ganhou. Consolando.", "The house wins this one.", "Dessa vez a casa ganhou."),
        (PartnerLine.RouletteNeedBet, "Mandaram girar sem aposta na mesa.", "Put a bet down first.", "Aposta alguma coisa primeiro."),
        (PartnerLine.RouletteBetDown, "Colocou as fichas da aposta no pano.", "Bet's down.", "Aposta feita."),
        (PartnerLine.PokerCallOrFold, "Hold'em: cartas e flop na mesa, o operador decide.", "Call or fold?", "Paga ou desiste?"),
        (PartnerLine.PokerNotQualified, "Hold'em: a mão dela não qualificou (menos que par de 4).", "I don't qualify. Your ante pays.", "Não qualifiquei. Sua ante ganha."),
        (PartnerLine.PokerFolded, "Hold'em: o operador desistiu da mão.", "Folding? Okay.", "Desistiu? Tá bom."),
        (PartnerLine.CounterExchanged, "No caixa: trocou fichas por moedas de prêmio.", "Here are your coins.", "Aqui estão suas moedas."),
        (PartnerLine.CounterNoChips, "No caixa sem fichas pra trocar.", "We don't have any chips to exchange.", "A gente não tem ficha pra trocar."),
        (PartnerLine.PrizeBought, "Comprou um prêmio no balcão. Feliz.", "It's ours!", "É nosso!"),
        (PartnerLine.PrizeTooExpensive, "Não tem moedas suficientes pro prêmio.", "We need more coins for that.", "Precisa de mais moedas pra isso."),
        (PartnerLine.PrizeOwned, "Quis comprar um prêmio que já tem.", "We already have that one.", "Essa a gente já tem."),
        (PartnerLine.OutfitLocked, "Pediram uma roupa que é prêmio do cassino e ainda não foi comprada.", "I don't have that... it's a prize at the casino.", "Eu não tenho essa... é prêmio do cassino."),
        (PartnerLine.NoCounter, "Mandaram trocar fichas / comprar fora do cassino.", "There's no prize counter here.", "Não tem balcão de prêmios aqui."),
        (PartnerLine.OperatorAway, "O operador fechou o programa das câmeras (ela fica sozinha numa zona segura, senta no chão).", "Okay... I'll wait here. Don't take too long.", "Tá... eu espero aqui. Não demora."),
        (PartnerLine.OperatorBack, "O operador voltou para as câmeras; ela se levanta.", "There you are. Let's keep going.", "Aí está você. Vamos continuar."),
        (PartnerLine.TagPatient, "Perguntaram o que tá escrito na plaquinha (roupa de paciente). Ela olha pro peito e lê devagar: 2716 dígito por dígito (two-seven-one-six / dois, sete, um, seis), leito 103 (one-oh-three / cento e três).",
            "It says... patient 2716. Bed 103.", "Tá escrito... paciente 2716. Leito 103."),
        (PartnerLine.TagPrison, "Perguntaram o que tá escrito na plaquinha (uniforme de prisão). Ela lê o número dígito por dígito (zero-nine-one-seven / zero, nove, um, sete), meio incomodada.",
            "0917. That's all it says.", "0917. Só isso."),
        (PartnerLine.NoTag, "Perguntaram o número da plaquinha, mas a roupa dela não tem nenhuma.", "I'm not wearing a tag.", "Eu não tô com plaquinha nenhuma."),
        (PartnerLine.OutfitBunny, "Vestiu a roupa de coelhinha (o prêmio mais caro do cassino). Sem graça, mas entrando na brincadeira.", "...Seriously? You're lucky I like you.", "...Sério? Sorte sua que eu gosto de você."),
    };

    [MenuItem("Last Terminal/5 - Assign Helena Voice Clips")]
    public static void AssignVoiceClips()
    {
        VoiceLineBank bank = EnsureVoiceBank();
        var report = new StringBuilder("[Voice] Clips assigned:");
        foreach (GameLanguage language in new[] { GameLanguage.English, GameLanguage.Portuguese })
        {
            string folder = LanguageFolder(language);
            EnsurePath(folder);
            // file name = line id, optionally _01, _02... (takes)
            var byLine = new Dictionary<PartnerLine, List<AudioClip>>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                int cut = file.LastIndexOf('_');
                string id = cut > 0 && int.TryParse(file.Substring(cut + 1), out _) ? file.Substring(0, cut) : file;
                if (!Enum.TryParse(id, out PartnerLine line)) { Debug.LogWarning($"[Voice] {path}: no line called '{id}'."); continue; }
                if (!byLine.TryGetValue(line, out List<AudioClip> list)) byLine.Add(line, list = new List<AudioClip>());
                list.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }
            int missing = 0;
            foreach (VoiceLineBank.Entry e in bank.Entries)
            {
                AudioClip[] clips = byLine.TryGetValue(e.Line, out List<AudioClip> l) ? l.OrderBy(c => c.name).ToArray() : new AudioClip[0];
                e.EditorSetClips(language, clips);
                if (clips.Length == 0) missing++;
            }
            report.Append($"  {(language == GameLanguage.Portuguese ? "PT" : "EN")}: {bank.Entries.Count - missing}/{bank.Entries.Count} lines");
        }
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
        Debug.Log(report + " (lines without a recording show as subtitles only).");
    }

    [MenuItem("Last Terminal/6 - Export Voice Script")]
    public static void ExportScript()
    {
        VoiceLineBank bank = EnsureVoiceBank();
        EnsurePath(VoiceAudioFolder);
        File.WriteAllText(ScriptPath, BuildScript(bank), Encoding.UTF8);
        AssetDatabase.ImportAsset(ScriptPath);
        Debug.Log("[Voice] Script written: " + ScriptPath);
    }

    private static string BuildScript(VoiceLineBank bank)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Helena - roteiro de falas");
        sb.AppendLine();
        sb.AppendLine("Grave cada fala num arquivo separado (WAV, mono, 44.1 ou 48 kHz), com o nome do arquivo = ID da fala.");
        sb.AppendLine("Mais de um take da mesma fala: `_01`, `_02`... (o jogo sorteia entre eles).");
        sb.AppendLine("Português em `Assets/Audio/Voice/Helena/PT/`, inglês em `Assets/Audio/Voice/Helena/EN/`. Depois: Last Terminal > 5.");
        sb.AppendLine();
        sb.AppendLine("| ID (nome do arquivo) | Quando ela fala | Português | English |");
        sb.AppendLine("|---|---|---|---|");
        foreach (VoiceLineBank.Entry e in bank.Entries)
            sb.AppendLine($"| `{e.Line}_01` | {e.Context} | {e.Text(GameLanguage.Portuguese)} | {e.Text(GameLanguage.English)} |");
        return sb.ToString();
    }
}
