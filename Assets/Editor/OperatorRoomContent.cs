using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// The first content of his room (Last Terminal/16): the files and e-mails of the fake OS, the hidden terminal commands,
// the photos of the evidence boards. Assets are created only when missing (Assets/Data/OperatorRoom), never
// overwritten: edit them freely. The board items are only created on a board that has none yet.
internal static class OperatorRoomContent
{
    // Asset folders of the files, e-mails and pictures.
    internal const string DataFolder = "Assets/Data/OperatorRoom";
    internal const string FilesFolder = DataFolder + "/Files";
    internal const string MailFolder = DataFolder + "/Emails";
    internal const string PicFolder = "Assets/Art/UI/OperatorOS";

    // Folder names of the fake OS per language.
    private const string Docs = "Documents", DocsPt = "Documentos", Pics = "Pictures", PicsPt = "Imagens";

    // ------------------------------------------------------------------ files
    private class FileDef
    {
        // Id, file name / title / text / folder per language, story flag needed, picture, in the recycle bin.
        public string id, fileEn, filePt, titleEn, titlePt, flag, textEn, textPt, folderEn, folderPt, picture;
        public bool recycled;
    }

    // The files of the fake OS.
    private static readonly FileDef[] FileDefs =
    {
        new FileDef { id = "01_leiame", fileEn = "README.txt", filePt = "LEIA-ME.txt", titleEn = "Terminal 07", titlePt = "Terminal 07",
            folderEn = Docs, folderPt = DocsPt,
            textEn = "OPERATIONS TERMINAL 07 - INSTRUCTIONS\n\n1. This terminal is wired into every camera of the building.\n2. The person on the ground hears your voice. Speak clearly.\n3. Cameras: open CCTV.EXE.\n4. The door of this room opens with a code. You will get the code when it is necessary.\n5. Do not shut the terminal down while she is exposed.\n\n- Inside contact",
            textPt = "TERMINAL DE OPERAÇÕES 07 - INSTRUÇÕES\n\n1. Este terminal está ligado a todas as câmeras do prédio.\n2. A pessoa em campo ouve a sua voz. Fale com clareza.\n3. Câmeras: abra o CCTV.EXE.\n4. A porta desta sala abre com um código. Você vai receber o código quando for necessário.\n5. Não desligue o terminal com ela exposta.\n\n- Contato interno" },
        new FileDef { id = "02_turno", fileEn = "shift_07.log", filePt = "turno_07.log", titleEn = "System log", titlePt = "Registro do sistema",
            folderEn = Docs, folderPt = DocsPt,
            textEn = "[19:58] Terminal 07 powered on remotely.\n[20:12] Vehicle arrived. Back entrance.\n[20:31] Operator placed in room 07. Sedated.\n[20:32] Door 07: LOCKED.\n[20:33] Camera grid: waiting for the field link.\n[03:12] Operator awake.",
            textPt = "[19:58] Terminal 07 ligado remotamente.\n[20:12] Veículo chegou. Entrada dos fundos.\n[20:31] Operador acomodado na sala 07. Sedado.\n[20:32] Porta 07: TRANCADA.\n[20:33] Rede de câmeras: aguardando o link de campo.\n[03:12] Operador acordado." },
        new FileDef { id = "03_obra", fileEn = "site_report_L3.txt", filePt = "relatorio_obra_N3.txt", titleEn = "Site report - level -3", titlePt = "Relatório de obra - nível -3",
            folderEn = Docs, folderPt = DocsPt,
            textEn = "SITE REPORT - LEVEL -3\n\nExcavation: complete.\nReinforcement of the walls: complete. Thickness as requested (unusual).\nVentilation: only toward the inside.\nNight deliveries: 14 this month. Not to be recorded.\nThe crew asks again what the rooms are for. No answer was given.",
            textPt = "RELATÓRIO DE OBRA - NÍVEL -3\n\nEscavação: concluída.\nReforço das paredes: concluído. Espessura conforme pedido (fora do comum).\nVentilação: só para dentro.\nEntregas noturnas: 14 este mês. Não registrar.\nA equipe pergunta de novo para que servem as salas. Nenhuma resposta foi dada." },
        new FileDef { id = "04_hospicio", fileEn = "asylum.jpg", filePt = "hospicio.jpg", folderEn = Pics, folderPt = PicsPt, picture = "T_OpOS_PicAsylum",
            textEn = "Taken from the mountain road. Someone was here before me.", textPt = "Tirada da estrada da serra. Alguém esteve lá antes de mim." },
        new FileDef { id = "05_cam_ext", fileEn = "cam_ext_2.jpg", filePt = "cam_ext_2.jpg", folderEn = Pics, folderPt = PicsPt, picture = "T_OpOS_PicSite",
            textEn = "03:12. Headlights at the construction site.", textPt = "03:12. Faróis no terreno da obra." },
        new FileDef { id = "06_planta", fileEn = "partial_plan.png", filePt = "planta_parcial.png", folderEn = Pics, folderPt = PicsPt, picture = "T_OpOS_PicBlueprint",
            textEn = "Level -3. One of the rooms is marked with a question mark.", textPt = "Nível -3. Uma das salas está marcada com um ponto de interrogação." },
        new FileDef { id = "07_orcamento", fileEn = "budget_scan.jpg", filePt = "orcamento_scan.jpg", folderEn = Pics, folderPt = PicsPt, picture = "T_OpOS_PicBudget",
            textEn = "5,248,800. Approved. The line in the middle was blacked out by hand.", textPt = "R$ 5.248.800,00. Aprovado. A linha do meio foi riscada à mão." },
        new FileDef { id = "08_rascunho", fileEn = "draft.txt", filePt = "rascunho.txt", titleEn = "", titlePt = "", recycled = true, folderEn = Docs, folderPt = DocsPt,
            textEn = "if you found this, you're the new one.\ndon't trust who brought you here.\nthe door has a code. the code is not the way out.",
            textPt = "se você achou isso, você é o novo.\nnão confia em quem te trouxe até aqui.\na porta tem código. o código não é a saída." },
        new FileDef { id = "09_recorte", fileEn = "clipping.jpg", filePt = "recorte.jpg", recycled = true, folderEn = Pics, folderPt = PicsPt, picture = "T_OpOS_PicNewspaper",
            textEn = "An old clipping. Somebody tried to delete it.", textPt = "Um recorte antigo. Alguém tentou apagar." },
    };

    // Creates the missing file assets.
    internal static List<OperatorFile> EnsureFiles()
    {
        EnsureFolders();
        for (int i = 0; i < FileDefs.Length; i++)
        {
            FileDef f = FileDefs[i];
            string path = $"{FilesFolder}/{f.id}.asset";
            if (AssetDatabase.LoadAssetAtPath<OperatorFile>(path) != null) continue;
            var file = ScriptableObject.CreateInstance<OperatorFile>();
            file.EditorSet(f.fileEn, f.filePt, f.titleEn ?? "", f.titlePt ?? "", f.textEn, f.textPt, f.flag ?? "", i * 10);
            Texture2D pic = f.picture != null ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{PicFolder}/{f.picture}.png") : null;
            file.EditorSetPlace(f.picture != null ? OperatorFile.Kind.Image : OperatorFile.Kind.Text, pic, f.folderEn, f.folderPt, f.recycled);
            AssetDatabase.CreateAsset(file, path);
        }
        // the pictures in each language (the art has words in it): set on every file, new or not
        foreach (FileDef f in FileDefs)
        {
            if (f.picture == null) continue;
            var file = AssetDatabase.LoadAssetAtPath<OperatorFile>($"{FilesFolder}/{f.id}.asset");
            if (file == null) continue;
            file.EditorSetPictures(AssetDatabase.LoadAssetAtPath<Texture2D>($"{PicFolder}/{f.picture}.png"),
                                   AssetDatabase.LoadAssetAtPath<Texture2D>($"{PicFolder}/{f.picture}_PT.png"));
        }
        return AssetDatabase.FindAssets("t:OperatorFile", new[] { FilesFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<OperatorFile>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(f => f != null).OrderBy(f => f.Order).ToList();
    }

    // ------------------------------------------------------------------ e-mails
    private static readonly (string id, string from, string address, string subjectEn, string subjectPt, string date, string flag, string textEn, string textPt, string attach)[] MailDefs =
    {
        ("01_michael", "Governador Michael", "gabinete@governo.gov.br", "Welcome back", "Bem-vindo de volta", "20:05", "",
         "If you're reading this, you got there in one piece. I'm sorry about the ride. It wasn't how I wanted it, but nobody could know where you were going. Not even you.\n\nHelena will reach you through the camera program. Be her eyes. Like before.\n\nAnd don't do anything stupid with that door.\n\n- M.",
         "Se você está lendo isso, chegou inteiro. Desculpa pelo transporte. Não era como eu queria, mas ninguém podia saber pra onde você ia. Nem você.\n\nA Helena vai falar com você pelo programa das câmeras. Seja os olhos dela. Como antes.\n\nE não faça nenhuma besteira com aquela porta.\n\n- M.", "01_leiame"),
        ("02_mary", "Mary", "mary.secretaria@governo.gov.br", "Your things", "Suas coisas", "20:40", "",
         "Hi. You left your coat and your keys in the governor's office. I'll keep them in my drawer until you're back.\n\nI hope it's not cold where you are.\n\nGood luck.\nMary",
         "Oi. Você esqueceu o casaco e as chaves no gabinete do governador. Vou guardar na minha gaveta até você voltar.\n\nEspero que não esteja frio aí onde você está.\n\nBoa sorte.\nMary", null),
        ("03_contato", "(no sender)", "-", "rules", "regras", "03:12", "",
         "1. don't try the door.\n2. don't shut it down when she needs you.\n3. i see what you see.\n\nthe code exists. you'll get it when you earn it.",
         "1. não tente a porta.\n2. não desligue quando ela precisar de você.\n3. eu vejo o que você vê.\n\no código existe. você ganha quando merecer.", null),
        ("04_contato_codigo", "(no sender)", "-", "smart", "esperto", "--:--", "operator_door_code",
         "it wasn't time yet.\nbut since you know the code: the door was never the problem.\nthe problem is what's on the other side.",
         "ainda não era a hora.\nmas já que você sabe o código: a porta nunca foi o problema.\no problema é o que tem do outro lado.", null),
    };

    // Creates the missing e-mail assets.
    internal static List<OperatorEmail> EnsureEmails()
    {
        EnsureFolders();
        for (int i = 0; i < MailDefs.Length; i++)
        {
            var m = MailDefs[i];
            string path = $"{MailFolder}/{m.id}.asset";
            if (AssetDatabase.LoadAssetAtPath<OperatorEmail>(path) != null) continue;
            OperatorFile attached = m.attach != null ? AssetDatabase.LoadAssetAtPath<OperatorFile>($"{FilesFolder}/{m.attach}.asset") : null;
            var mail = ScriptableObject.CreateInstance<OperatorEmail>();
            mail.EditorSet(m.from, m.address, m.subjectEn, m.subjectPt, m.date, m.textEn, m.textPt, m.flag, i * 10, attached);
            AssetDatabase.CreateAsset(mail, path);
        }
        return AssetDatabase.FindAssets("t:OperatorEmail", new[] { MailFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<OperatorEmail>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(m => m != null).OrderBy(m => m.Order).ToList();
    }

    // Creates the data folders.
    private static void EnsureFolders()
    {
        EditorAssets.EnsureFolder("Assets", "Data");
        EditorAssets.EnsureFolder("Assets/Data", "OperatorRoom");
        EditorAssets.EnsureFolder(DataFolder, "Files");
        EditorAssets.EnsureFolder(DataFolder, "Emails");
    }

    // ------------------------------------------------------------------ the terminal's hidden commands
    internal static OperatorOS.TerminalEntry[] TerminalEntries => new[]
    {
        new OperatorOS.TerminalEntry { command = "door|porta|door07|porta07",
            responseEn = "DOOR 07\nMAGNETIC LOCK: ACTIVE\nCODE: ****\nLAST OPENED: 20:31 (from outside)",
            responsePt = "PORTA 07\nTRAVA MAGNÉTICA: ATIVA\nCÓDIGO: ****\nÚLTIMA ABERTURA: 20:31 (por fora)" },
        new OperatorOS.TerminalEntry { command = "helena",
            responseEn = "RESTRICTED FILE.\nAuthorization level 3 required.", responsePt = "ARQUIVO RESTRITO.\nNível de autorização 3 necessário.", setFlag = "terminal_helena" },
        new OperatorOS.TerminalEntry { command = "asylum|hospicio|hospício|project|projeto",
            responseEn = "PROJECT: [REDACTED]\nBUDGET: 5,248,800.00\nWORK: BASEMENT - LEVEL -3\nSTATUS: IN PROGRESS",
            responsePt = "PROJETO: [REDIGIDO]\nORÇAMENTO: R$ 5.248.800,00\nOBRA: SUBSOLO - NÍVEL -3\nSTATUS: EM ANDAMENTO", setFlag = "terminal_project" },
        new OperatorOS.TerminalEntry { command = "michael|mike|governador|governor",
            responseEn = "michael_private.txt - password required.", responsePt = "michael_privado.txt - senha necessária." },
        new OperatorOS.TerminalEntry { command = "0312|03:12",
            responseEn = "...", responsePt = "...", setFlag = "terminal_0312" },
    };

    // ------------------------------------------------------------------ the evidence boards
    // an item: the rectangle on the board's picture (pixels of the texture, from its top-left) or, on a board of
    // unknown layout, in 0..1 of its face (u from the left, v from the top); title / text / thought EN + PT
    internal class Item
    {
        // Rectangle on the board picture (or 0..1 of its face).
        public Rect rect;
        // Title / text / thought per language.
        public string titleEn, titlePt, textEn, textPt, thoughtEn, thoughtPt;
    }

    // Size of the main board picture and its items.
    internal static readonly Vector2 MainTexture = new Vector2(768f, 512f);
    internal static readonly Item[] MainBoard =
    {
        new Item { rect = Rect.MinMaxRect(0, 0, 185, 95), titleEn = "THE CREW", titlePt = "A EQUIPE",
            textEn = "Six faces from the asylum's construction crew. None of them is on any state payroll.", textPt = "Seis rostos da equipe da obra do hospício. Nenhum deles aparece em folha de pagamento do estado.",
            thoughtEn = "Who hires people that don't exist?", thoughtPt = "Quem contrata gente que não existe?" },
        new Item { rect = Rect.MinMaxRect(268, 5, 350, 95), titleEn = "THE PLAN", titlePt = "O PLANO",
            textEn = "Someone's plan, in a handwriting I don't know. Find the pattern. Expose the weak spot.", textPt = "O plano de alguém, numa letra que eu não conheço. Achar o padrão. Expor o ponto fraco.",
            thoughtEn = "Was this written for me?", thoughtPt = "Isso foi escrito pra mim?" },
        new Item { rect = Rect.MinMaxRect(300, 165, 470, 300), titleEn = "THE MAP", titlePt = "O MAPA",
            textEn = "The region around the asylum: the mountain road, the forest, one way in. Every line on the board ends here.", textPt = "A região do hospício: a estrada da serra, a floresta, uma entrada só. Todas as linhas do quadro terminam aqui.",
            thoughtEn = "Mountains and forest. Nobody hears anything out there.", thoughtPt = "Montanha e floresta. Ninguém escuta nada lá." },
        new Item { rect = Rect.MinMaxRect(690, 0, 768, 115), titleEn = "CLIPPING", titlePt = "RECORTE",
            textEn = "A money story. Someone circled the number and wrote over it: 5,248,800.", textPt = "Uma matéria sobre dinheiro. Alguém circulou o valor e escreveu por cima: R$ 5.248.800,00.",
            thoughtEn = "Five million. Mike wasn't exaggerating.", thoughtPt = "Cinco milhões. O Mike não exagerou." },
        new Item { rect = Rect.MinMaxRect(510, 215, 612, 282), titleEn = "SURVEILLANCE", titlePt = "VIGILÂNCIA",
            textEn = "Photos from a parked car: a van with no plates going into the site. Every photo says 03:12.", textPt = "Fotos tiradas de um carro parado: uma van sem placa entrando no terreno. Todas dizem 03:12.",
            thoughtEn = "Always the same time.", thoughtPt = "Sempre a mesma hora." },
        new Item { rect = Rect.MinMaxRect(538, 358, 616, 442), titleEn = "NOTE", titlePt = "BILHETE",
            textEn = "\"If you talk, you die.\"", textPt = "\"Se você falar, você morre.\"",
            thoughtEn = "It's not a warning for them. It's for whoever reads it.", thoughtPt = "Não é um aviso pra eles. É pra quem ler." },
        new Item { rect = Rect.MinMaxRect(22, 420, 78, 485), titleEn = "NOTE", titlePt = "BILHETE",
            textEn = "\"Trust no one.\" Pinned over everything else.", textPt = "\"Não confie em ninguém.\" Pregado por cima de todo o resto.",
            thoughtEn = "Not even Mike?", thoughtPt = "Nem o Mike?" },
        new Item { rect = Rect.MinMaxRect(122, 328, 182, 398), titleEn = "PRINTS", titlePt = "DIGITAIS",
            textEn = "Seven sets of fingerprints from the site. One of them is crossed out in red.", textPt = "Sete conjuntos de digitais da obra. Um deles está riscado de vermelho.",
            thoughtEn = "Crossed out. Like a name on a list.", thoughtPt = "Riscado. Como um nome numa lista." },
    };

    // Size of the old board picture and its items.
    internal static readonly Vector2 OldTexture = new Vector2(736f, 489f);
    internal static readonly Item[] OldBoard =
    {
        new Item { rect = Rect.MinMaxRect(303, 178, 407, 247), titleEn = "OLD CASE - CLIPPING", titlePt = "CASO ANTIGO - RECORTE",
            textEn = "A headline from two years ago. The date was circled.", textPt = "Uma manchete de dois anos atrás. A data foi circulada.",
            thoughtEn = "Two years. Feels longer.", thoughtPt = "Dois anos. Parece mais." },
        new Item { rect = Rect.MinMaxRect(486, 175, 570, 278), titleEn = "OLD CASE - PHOTO", titlePt = "CASO ANTIGO - FOTO",
            textEn = "A face behind a hood. Taken from far away.", textPt = "Um rosto atrás de um capuz. Tirada de longe.",
            thoughtEn = "I don't want to look at this one.", thoughtPt = "Não quero olhar pra essa." },
        new Item { rect = Rect.MinMaxRect(570, 225, 705, 335), titleEn = "OLD CASE - MAP", titlePt = "CASO ANTIGO - MAPA",
            textEn = "An old map with a route marked by hand.", textPt = "Um mapa velho com um caminho marcado à mão.",
            thoughtEn = "We took that road. I remember.", thoughtPt = "A gente fez esse caminho. Eu lembro." },
        new Item { rect = Rect.MinMaxRect(303, 40, 378, 168), titleEn = "OLD CASE - FACES", titlePt = "CASO ANTIGO - ROSTOS",
            textEn = "Small photos, one for each person involved.", textPt = "Fotos 3x4, uma pra cada pessoa envolvida.",
            thoughtEn = "Too many faces. Too few answers.", thoughtPt = "Rosto demais. Resposta de menos." },
    };

    // a board of unknown layout (u, v of its face, from the top-left)
    internal static readonly Item[] GenericOldBoard =
    {
        new Item { rect = Rect.MinMaxRect(0.08f, 0.1f, 0.38f, 0.45f), titleEn = "OLD CASE", titlePt = "CASO ANTIGO",
            textEn = "Notes from the old case.", textPt = "Anotações do caso antigo.", thoughtEn = "I thought I'd burned all of this.", thoughtPt = "Achei que tinha queimado tudo isso." },
        new Item { rect = Rect.MinMaxRect(0.4f, 0.3f, 0.62f, 0.7f), titleEn = "OLD CASE", titlePt = "CASO ANTIGO",
            textEn = "Photos from the old case.", textPt = "Fotos do caso antigo.", thoughtEn = "Someone kept everything.", thoughtPt = "Alguém guardou tudo." },
        new Item { rect = Rect.MinMaxRect(0.66f, 0.15f, 0.92f, 0.55f), titleEn = "OLD CASE", titlePt = "CASO ANTIGO",
            textEn = "A list of dates. The last one is two years ago.", textPt = "Uma lista de datas. A última é de dois anos atrás.", thoughtEn = "That night.", thoughtPt = "Aquela noite." },
    };

    // SM_Ini_CorkBoard (above the desk): local rectangles of its front (meters, x from its pivot, y up)
    internal static readonly Item[] DeskBoard =
    {
        new Item { rect = Rect.MinMaxRect(-0.23f, 0.40f, 0.31f, 0.79f), titleEn = "NEWSPAPERS", titlePt = "JORNAIS",
            textEn = "The governor's first week in office, on two front pages.", textPt = "A primeira semana do governador, em duas capas de jornal.",
            thoughtEn = "He smiles the same way in every photo.", thoughtPt = "Ele sorri do mesmo jeito em todas as fotos." },
        new Item { rect = Rect.MinMaxRect(0.15f, 0.06f, 0.61f, 0.38f), titleEn = "POLAROIDS", titlePt = "POLAROIDES",
            textEn = "Polaroids of the asylum's gate, one per night, from the same spot.", textPt = "Polaroides do portão do hospício, uma por noite, do mesmo lugar.",
            thoughtEn = "Someone stood there every night. Watching.", thoughtPt = "Alguém ficou parado ali toda noite. Vigiando." },
    };
}
