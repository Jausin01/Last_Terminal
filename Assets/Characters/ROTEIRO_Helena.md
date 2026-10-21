# Helena - roteiro de falas

Grave cada fala num arquivo separado (WAV, mono, 44.1 ou 48 kHz), com o nome do arquivo = ID da fala.
Mais de um take da mesma fala: `_01`, `_02`... (o jogo sorteia entre eles).
Português em `Assets/Audio/Voice/Helena/PT/`, inglês em `Assets/Audio/Voice/Helena/EN/`. Depois: Lifeline > 5.

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
