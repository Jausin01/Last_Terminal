# Helena - roteiro de falas

Grave cada fala num arquivo separado (WAV, mono, 44.1 ou 48 kHz), com o nome do arquivo = ID da fala.
Mais de um take da mesma fala: `_01`, `_02`... (o jogo sorteia entre eles).
Português em `Assets/Audio/Voice/Helena/PT/`, inglês em `Assets/Audio/Voice/Helena/EN/`. Depois: Last Terminal > 5.

| ID (nome do arquivo) | Quando ela fala | Português | English |
|---|---|---|---|
| `Acknowledge_01` | Recebeu uma ordem e vai fazer (pegar, abrir porta, atacar). | Tá bom. | Okay. |
| `OnMyWay_01` | Começa a andar até onde mandaram. | Tô indo. | On my way. |
| `NotUnderstood_01` | Não entendeu o que o operador falou. | Hã? Não entendi. | What? I didn't get that. |
| `Busy_01` | Está no meio de outra coisa (sentada, abrindo porta, pegando item). | Agora não, espera. | Not now, hold on. |
| `CantGetThere_01` | Não tem caminho até o lugar. | Não consigo chegar lá daqui. | I can't get there from here. |
| `NothingThere_01` | Procurou o que pediram e não achou. | Não tô vendo nada disso. | I don't see anything like that. |
| `GotIt_01` | Pegou o item. | Peguei. | Got it. |
| `TookSome_01` | Só coube parte do item na bolsa. | Não coube tudo. | I can't carry all of it. |
| `BagFull_01` | Inventário cheio. | Não tenho mais espaço. | I can't carry any more. |
| `CantReach_01` | O item está fora de alcance. | Não alcanço. | I can't reach it. |
| `DoorLocked_01` | Forçou a maçaneta e a porta está trancada. | Tá trancada. | It's locked. |
| `DoorNeedsOperator_01` | Porta de segurança: pede para o operador abrir pelas câmeras. | Não abre. Você consegue abrir daí? | It won't open. Can you open it from there? |
| `DoorUnlocked_01` | Destrancou a porta com a chave/cartão. | A chave funcionou. | The key worked. |
| `DoorAlreadyOpen_01` | Mandaram abrir uma porta já aberta. | Já tá aberta. | It's already open. |
| `DoorAlreadyClosed_01` | Mandaram fechar uma porta já fechada. | Já tá fechada. | It's already closed. |
| `Healed_01` | Usou o kit médico. Alívio. | Bem melhor. | That's better. |
| `NotHurt_01` | Mandaram se curar mas ela está bem. | Eu tô bem, não preciso. | I'm not hurt. |
| `NoHealing_01` | Mandaram se curar e não tem kit. | Não tenho nada pra me curar. | I don't have anything to heal with. |
| `WeaponReady_01` | Sacou a arma. Tensa, focada. | Pronta. | Ready. |
| `WeaponAway_01` | Guardou a arma. | Guardando. | Putting it away. |
| `DontHaveThat_01` | Pediram uma arma ou item que ela não tem. | Eu não tenho isso. | I don't have that. |
| `NoTarget_01` | Mandaram atirar/atacar e não tem alvo. | Não tem nada pra atirar. | There's nothing to shoot. |
| `OutOfAmmo_01` | Sem munição. Urgente, nervosa. | Tô sem munição! | I'm out of ammo! |
| `Reloading_01` | Começa a recarregar. | Recarregando. | Reloading. |
| `AlreadyLoaded_01` | Mandaram recarregar e a arma está cheia. | Já tá carregada. | It's already loaded. |
| `NoGun_01` | Mandaram atirar/recarregar sem arma de fogo na mão. | Eu não tô com arma na mão. | I'm not holding a gun. |
| `Saving_01` | Vai até o computador salvar. | Vou salvar aqui. | I'll save here. |
| `NoSavePoint_01` | Não tem computador por perto. | Não tem nenhum computador por aqui. | There's no computer around here. |
| `StatusFine_01` | Perguntaram como ela está: inteira. | Tô bem. | I'm fine. |
| `StatusHurt_01` | Perguntaram como ela está: machucada, aguentando. | Tô machucada, mas dá pra continuar. | I'm hurt, but I can keep going. |
| `StatusBadlyHurt_01` | Perguntaram como ela está: muito ferida, voz fraca. | Tô muito machucada... | I'm hurt badly... |
| `Stopping_01` | Mandaram parar. | Parei. | Stopping. |
| `CombatStart_01` | Viu inimigos, começa o combate. Susto, alerta. | Tem alguma coisa aqui! | Something's here! |
| `TargetDown_01` | Derrubou o alvo que estava atacando. Ofegante. | Derrubei. | It's down. |
| `Aiming_01` | Mirou no alvo que o operador escolheu. | Tô na mira. | I've got it. |
| `NoSuchTarget_01` | O operador falou um número de inimigo que não existe. | Qual? Não vejo esse número. | Which one? I don't see that number. |
| `BjAccept_01` | O operador chamou pra jogar. Animada, topa na hora. | Um joguinho? Bora, por que não. | A game? Sure, why not. |
| `BjNoTable_01` | Chamou pra jogar mas não tem mesa por perto. | Não tem mesa nenhuma por aqui. | There's no table around here. |
| `BjNoGame_01` | Falou de aposta sem estar jogando. | A gente nem tá jogando. | We're not playing anything. |
| `BjPlaceBet_01` | Sentada na mesa, pede a aposta. Tom de dealer. | Faça sua aposta. | Place your bet. |
| `BjBetLimits_01` | Aposta fora do limite da mesa. | Limite da mesa, foi mal. | Table limits, sorry. |
| `BjNotEnough_01` | O operador não tem fichas pra isso. | Você não tem tudo isso. | You don't have that much. |
| `BjYourTurn_01` | Cartas dadas, vez do operador. | Carta ou para? | Hit or stand? |
| `BjInsurance_01` | Ela mostra um ás: oferece seguro. | Quer seguro? | Insurance? |
| `BjPeek_01` | Vai espiar a carta virada. Suspense. | Deixa eu ver... | Let me check... |
| `BjDealerBlackjack_01` | Ela tem blackjack. Fingindo pena. | Blackjack. Foi mal! | Blackjack. Sorry! |
| `BjPlayerBlackjack_01` | O operador tirou blackjack. Surpresa. | Blackjack! Que sorte, hein. | Blackjack! Lucky you. |
| `BjPlayerBust_01` | O operador estourou. Provocando. | Estourou. Ganancioso, hein? | Bust. Too greedy. |
| `BjDealerBust_01` | Ela estourou. Frustrada. | Estourei... droga. | I bust... ugh. |
| `BjPlayerWins_01` | O operador ganhou a mão. | Você ganhou. | You win. |
| `BjHouseWins_01` | Ela ganhou a mão. Convencida. | A casa ganha. | House wins. |
| `BjPush_01` | Empate. | Empate. | Push. |
| `BjCountingCards_01` | O operador ganhou 3 seguidas. Desconfiada, brincando. | Você tá contando cartas, né? | Are you counting cards? |
| `BjAnotherRound_01` | Fim da mão: pergunta se joga mais uma. | Mais uma? | Another round? |
| `BjFinishHand_01` | Quis parar ou sair no meio da mão. | Vamos terminar essa mão primeiro. | Let's finish this hand first. |
| `BjGameOver_01` | Pararam de jogar, ela guarda o baralho e levanta. | Foi divertido. De volta ao trabalho. | That was fun. Back to work. |
| `BjBroke_01` | O operador ficou sem fichas: ela dá umas de presente. | Você quebrou. Toma, essa é por conta da casa. | You're broke. Here, on the house. |
| `BjIdle_01` | O operador está demorando pra decidir. | Pode pensar... eu não vou a lugar nenhum. | Take your time... I'm not going anywhere. |
| `BjCantDouble_01` | Pediu pra dobrar quando não dá. | Não dá pra dobrar agora. | You can't double now. |
| `BjCantSplit_01` | Pediu pra dividir uma mão que não é par. | Não dá pra dividir essa. | You can't split that. |
| `BjCantSurrender_01` | Pediu pra desistir depois da hora. | Tarde demais pra desistir. | Too late to surrender. |
| `BjShuffle_01` | Acabou o baralho, ela embaralha um novo. | Baralho novo. Deixa eu embaralhar. | New shoe. Let me shuffle. |
| `NotNow_01` | No meio do combate pediram outra coisa (abrir porta, jogar...). Ríspida, ofegante. | Agora não, eu tô lutando! | Not now, I'm fighting! |
| `Dodging_01` | Pula pra trás desviando de um golpe. Esforço. | Opa! | Whoa! |
| `Ducking_01` | Se abaixa rápido. | Abaixando! | Getting down! |
| `GettingUp_01` | Levanta depois de estar abaixada. | Levantando. | Getting up. |
| `BackingOff_01` | Se afasta do inimigo sem tirar os olhos dele. | Tô recuando! | Backing off! |
| `Retreating_01` | Foge do combate correndo pra porta. Desesperada. | Vou sair daqui! | I'm getting out of here! |
| `TakingCover_01` | Corre pro canto mais longe dos inimigos. | Vou me proteger! | Taking cover! |
| `HoldingFire_01` | Para de atirar mas continua mirando. | Segurando o fogo. | Holding fire. |
| `FireAtWill_01` | Liberada pra atirar em tudo. Determinada. | Fogo à vontade! | Firing at will! |
| `NoThreat_01` | Mandaram recuar/se proteger/fugir sem inimigo por perto. | Do quê? Não tem nada aqui. | From what? There's nothing here. |
| `NoWayOut_01` | Quer fugir/se proteger mas não tem pra onde ir. Medo. | Não tem pra onde ir! | There's nowhere to go! |
| `OutfitChanged_01` | Trocou de roupa na frente da câmera. Brincalhona, um pouco envergonhada. | Não espia! ...Pronto. | Don't peek! ...Okay, done. |
| `OutfitSame_01` | Pediram a roupa que ela já está usando. | Eu já tô com ela. | I'm already wearing it. |
| `OutfitBunny_01` | Vestiu a roupa de coelhinha (o prêmio mais caro do cassino). Sem graça, meio irritada com o operador (os dois ainda não se acertaram). | ...Sério? Você tá gostando disso demais. | ...Seriously? You're enjoying this way too much. |
| `TooFar_01` | Mandaram ela pra um lugar longe (não é a sala do lado). Pede pra guiar por partes. | É longe demais. Me guia uma sala de cada vez. | That's too far. Guide me one room at a time. |
| `LookWhat_01` | Parou na frente da câmera e olhou pra ela, esperando. Meio desconfiada. | Que foi? | What? |
| `LookWaiting_01` | Ficou um tempão olhando pra câmera e nada aconteceu. | ...Vai ficar só me olhando? | ...Are you just going to stare? |
| `NoCamera_01` | Mandaram olhar pra câmera mas a câmera que o operador vê não é da sala dela. | Qual câmera? Não tô vendo nenhuma daqui. | Which camera? I can't see it from here. |
| `Hurt_01` | Levou um golpe. Grito curto de dor (várias versões ajudam: _01, _02, _03). | Ai! | Agh! |
| `HurtBad_01` | Ferida feio, mancando. Fraca, assustada. | Tô muito machucada... preciso me cuidar. | I'm hurt bad... I need to patch myself up. |
| `Dying_01` | Golpe final, ela cai. Último suspiro. | Não... | No... |
| `StanceRanged_01` | Entra em posição de combate com a arma de fogo. Focada. | Em posição. Vou manter distância. | In position. I'll keep my distance. |
| `StanceMelee_01` | Em guarda com a arma branca. Tensa, esperando a ordem. | Em guarda. É só mandar. | On guard. Tell me when. |
| `StandingDown_01` | Sai da posição de combate. Solta o ar. | Tá... relaxando. | Okay... relaxing. |
| `Cornered_01` | Não tem mais pra onde recuar. Assustada. | Tô encurralada! | I'm cornered! |
| `KeypadOpen_01` | Digitou a senha certa e a porta abriu. Alívio. | Deu certo... abriu! | It worked... it's open! |
| `KeypadWrong_01` | O teclado apitou erro. Frustrada. | Senha errada. | Wrong code. |
| `NoKeypad_01` | Mandaram digitar mas não tem teclado por perto. | Não tem teclado aqui. | There's no keypad here. |
| `KeypadWhich_01` | Está no teclado esperando os números. | Tá. Qual é a senha? | Okay. What's the code? |
| `TurnSide_01` | Vira de lado pra câmera, meio sem graça. | Assim? | Like this? |
| `TurnBack_01` | Vira de costas pra câmera. Desconfiada. | De costas? ...Tá. | My back? ...Okay. |
| `ElevatorGoing_01` | Vai pegar o elevador que o operador mandou. | Vou pegar o elevador. | Taking the elevator. |
| `ElevatorNeedKey_01` | O elevador precisa de uma chave colorida que ela não tem (a cor está na placa). | Precisa de uma chave. Da cor da placa. | It needs a key. The color on the sign. |
| `ElevatorOutOfService_01` | Esse elevador não leva a lugar nenhum por enquanto. | Esse aqui tá fora de serviço. | This one's out of service. |
| `NoElevator_01` | Mandaram pegar um elevador que não existe aqui. | Não tem elevador assim aqui. | There's no elevator like that here. |
| `SlotReady_01` | Chegou no caça-níquel. Animada, supersticiosa. | Tá... vamos ver se eu tô com sorte. | Okay... let's see if I'm lucky. |
| `SlotWin_01` | Ganhou algo no caça-níquel. Contente. | Ganhamos alguma coisa! | We won something! |
| `SlotJackpot_01` | Prêmio grande no caça-níquel. Eufórica. | Jackpot! Olha isso! | Jackpot! Look at that! |
| `SlotNoChips_01` | Acabaram as fichas pra apostar. | Acabaram as fichas. | We're out of chips. |
| `ElevatorArrived_01` | Saiu do elevador num lugar novo. Olhando em volta. | Chegamos. | Here we are. |
| `RouletteBets_01` | Chegou na roleta como crupiê. Tom de cassino, divertida. | Façam suas apostas. | Place your bets. |
| `RouletteNoMoreBets_01` | Girou a roleta. Clássico do crupiê. | Apostas encerradas! | No more bets! |
| `RouletteWin_01` | A bola caiu num número que deu lucro. Animada. | Ganhamos! | We won! |
| `RouletteLose_01` | A bola caiu e nada ganhou. Consolando. | Dessa vez a casa ganhou. | The house wins this one. |
| `RouletteNeedBet_01` | Mandaram girar sem aposta na mesa. | Aposta alguma coisa primeiro. | Put a bet down first. |
| `RouletteBetDown_01` | Colocou as fichas da aposta no pano. | Aposta feita. | Bet's down. |
| `PokerCallOrFold_01` | Hold'em: cartas e flop na mesa, o operador decide. | Paga ou desiste? | Call or fold? |
| `PokerNotQualified_01` | Hold'em: a mão dela não qualificou (menos que par de 4). | Não qualifiquei. Sua ante ganha. | I don't qualify. Your ante pays. |
| `PokerFolded_01` | Hold'em: o operador desistiu da mão. | Desistiu? Tá bom. | Folding? Okay. |
| `CounterExchanged_01` | No caixa: trocou fichas por moedas de prêmio. | Aqui estão suas moedas. | Here are your coins. |
| `CounterNoChips_01` | No caixa sem fichas pra trocar. | A gente não tem ficha pra trocar. | We don't have any chips to exchange. |
| `PrizeBought_01` | Comprou um prêmio no balcão. Feliz. | É nosso! | It's ours! |
| `PrizeTooExpensive_01` | Não tem moedas suficientes pro prêmio. | Precisa de mais moedas pra isso. | We need more coins for that. |
| `PrizeOwned_01` | Quis comprar um prêmio que já tem. | Essa a gente já tem. | We already have that one. |
| `OutfitLocked_01` | Pediram uma roupa que é prêmio do cassino e ainda não foi comprada. | Eu não tenho essa... é prêmio do cassino. | I don't have that... it's a prize at the casino. |
| `NoCounter_01` | Mandaram trocar fichas / comprar fora do cassino. | Não tem balcão de prêmios aqui. | There's no prize counter here. |
| `OperatorAway_01` | O operador fechou o programa das câmeras (ela fica sozinha numa zona segura, senta no chão). | Tá... eu espero aqui. Não demora. | Okay... I'll wait here. Don't take too long. |
| `OperatorBack_01` | O operador voltou para as câmeras; ela se levanta. | Aí está você. Vamos continuar. | There you are. Let's keep going. |
| `TagPatient_01` | Perguntaram o que tá escrito na plaquinha (roupa de paciente). Ela olha pro peito e lê devagar: 2716 dígito por dígito (two-seven-one-six / dois, sete, um, seis), leito 103 (one-oh-three / cento e três). | Tá escrito... paciente 2716. Leito 103. | It says... patient 2716. Bed 103. |
| `TagPrison_01` | Perguntaram o que tá escrito na plaquinha (uniforme de prisão). Ela lê o número dígito por dígito (zero-nine-one-seven / zero, nove, um, sete), meio incomodada. | 0917. Só isso. | 0917. That's all it says. |
| `NoTag_01` | Perguntaram o número da plaquinha, mas a roupa dela não tem nenhuma. | Eu não tô com plaquinha nenhuma. | I'm not wearing a tag. |
