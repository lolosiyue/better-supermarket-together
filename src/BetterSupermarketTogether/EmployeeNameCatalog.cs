using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterSupermarketTogether;

internal enum EmployeeGender
{
    Unknown,
    Male,
    Female
}

internal static class EmployeeNameCatalog
{
    internal const string MaleChinese = "陳大明,黃志豪,李文彬,吳俊賢,蔡明華,謝金龍,高志偉,潘文傑,洪建宏,曹志明,柯文謙,許文龍,唐國強,鍾明軒,江文雄,馬俊賢,宋小龍,徐志豪,鄭文謙,林建安";
    internal const string FemaleChinese = "林秀英,張雅婷,王美玲,劉怡芳,楊淑芬,何雅琴,賴淑惠,姜美鳳,曾雅惠,盧小燕,施雅婷,廖寶珠,鄧小鳳,沈春梅,蘇麗華,韓雪梅,白雪娥,方雅玲,袁美鳳,范雅芬";
    internal const string NeutralChinese = "陳子安,林思言,黃沐恩,張予安,李知秋,王允希,吳若林,劉以諾,蔡星辰,楊晨曦";
    internal const string MaleEnglish = "Alex Carter,James Parker,Daniel Brooks,Oliver Hayes,Noah Morgan,Liam Cooper,Ethan Walker,Lucas Bailey,Henry Adams,Jack Evans,Leo Thompson,Samuel Clark,Benjamin Scott,William Green,Thomas Baker,Charlie Lewis,Maxwell Miller,Oscar Taylor,Arthur James,George Russell";
    internal const string FemaleEnglish = "Emma Wilson,Olivia Bennett,Sophia Turner,Amelia Foster,Charlotte Reed,Mia Collins,Evelyn Price,Harper Mitchell,Isabella Hughes,Grace Harrison,Lily Anderson,Ella Roberts,Chloe Richardson,Zoe Phillips,Lucy Campbell,Hannah Edwards,Alice Stewart,Ruby Watson,Ivy Spencer,Nora Hamilton";
    internal const string NeutralEnglish = "Alex Morgan,Taylor Reed,Jordan Parker,Casey Brooks,Riley Carter,Avery Quinn,Robin Hayes,Jamie Collins,Sam Bailey,Morgan Ellis";

    private const string ChineseSurnames = "陳,林,黃,張,李,王,吳,劉,蔡,楊,許,鄭,謝,郭,洪,邱,曾,廖,賴,徐,周,葉,蘇,莊,呂,江,何,蕭,羅,高,潘,簡,朱,鍾,彭,游,詹,胡,施,沈,余,趙,盧,梁,顏,孫,魏,翁,戴,范,宋,方,鄧,杜,傅,侯,曹,薛,丁,卓,阮,蔣,唐,馬,石,董,紀,溫,藍,柯,姚,康,馮,程,倪,夏,田,陸,汪,白";
    private const string MaleChineseGiven = "子軒,宇翔,承翰,柏翰,冠宇,柏宇,哲宇,宇辰,宥廷,承恩,睿哲,品睿,子睿,彥廷,冠廷,俊傑,俊宏,俊廷,俊毅,俊宇,建宏,建豪,建廷,建霖,志豪,志宏,志偉,志遠,宗翰,宗佑,宗霖,明哲,明軒,明翰,明杰,文彬,文皓,文傑,文軒,柏均,柏睿,柏勳,柏霖,承佑,承澤,承軒,承宏,宇軒,宇恆,宇謙,宇哲,家豪,家宏,家佑,家瑋,家榮,家銘,家睿,冠霖,冠傑,冠勳,冠豪,冠銘,冠融,彥佑,彥霖,彥宏,彥傑,彥勳,彥辰,彥翔,睿軒,睿廷,睿翔,睿恩,睿宏,哲維,哲翰,哲銘,哲安,紹恩,紹宇,紹廷,紹軒,維倫,維軒,維哲,維恩,偉霖,偉哲,偉倫,偉翔,浩宇,浩然,浩軒,浩霖,皓宇,皓軒,皓哲,皓廷,庭宇,庭翰,庭佑,庭瑋,育誠,育翔,育霖,育廷,凱翔,凱文,凱傑,凱倫,凱瑞,信宏,信佑,信廷,信宇,博文,博宇,博翰,博睿";
    private const string FemaleChineseGiven = "雅婷,雅雯,雅涵,雅琪,雅筑,雅萱,雅馨,雅琳,雅晴,雅欣,怡君,怡婷,怡萱,怡潔,怡蓁,怡安,怡伶,怡瑄,怡靜,怡珊,欣怡,欣妤,欣瑜,欣潔,欣蓉,欣儀,欣芸,欣妍,欣彤,欣蕾,佳穎,佳蓉,佳玲,佳琪,佳慧,佳瑩,佳琳,佳芸,佳妤,佳儀,婉婷,婉瑜,婉如,婉君,婉萱,婉晴,婉琳,婉儀,婉柔,婉甄,淑芬,淑惠,淑華,淑玲,淑娟,淑敏,淑琪,淑儀,美玲,美惠,美芳,美娟,美琳,美瑩,美慧,美晴,詩涵,詩婷,詩穎,詩晴,詩妍,詩琪,詩雅,詩琳,詩芸,詩潔,思妤,思穎,思涵,思婷,思琪,思瑩,思琳,思妍,佩珊,佩蓉,佩琪,佩芸,佩瑜,佩儀,佩萱,佩如,芷晴,芷涵,芷妍,芷芸,芷瑄,芷寧,芷萱,芷彤,韻如,韻婷,韻涵,韻潔,韻萱,韻琳,韻詩,韻晴,雨彤,雨晴,雨柔,雨萱,雨潔,雨薇,雨桐,雨涵,曉雯,曉晴,曉琪,曉萱";
    private const string NeutralChineseGiven = "子安,子寧,子涵,子言,子晴,子希,子恆,子辰,子瑜,子恩,予安,予寧,予希,予恆,予晴,予恩,予辰,予然,予言,予澄,沐恩,沐晨,沐言,沐安,沐晴,沐希,沐寧,沐辰,沐然,沐澄,知秋,知夏,知遠,知安,知言,知行,知衡,知然,知澄,知禾,以諾,以安,以寧,以晴,以辰,以恆,以澄,以言,以然,以希,晨曦,晨安,晨希,晨恩,晨言,晨澄,晨寧,晨辰,晨然,晨禾,星辰,星言,星安,星澄,星希,星然,星寧,星禾,星晨,星予,允希,允安,允寧,允恆,允言,允恩,允澄,允然,允辰,允禾,若林,若言,若安,若晴,若希,若寧,若然,若辰,若澄,若禾";
    private const string EnglishSurnames = "Smith,Johnson,Williams,Brown,Jones,Garcia,Miller,Davis,Rodriguez,Martinez,Hernandez,Lopez,Gonzalez,Wilson,Anderson,Thomas,Taylor,Moore,Jackson,Martin,Lee,Perez,Thompson,White,Harris,Sanchez,Clark,Ramirez,Lewis,Robinson,Walker,Young,Allen,King,Wright,Scott,Torres,Nguyen,Hill,Flores,Green,Adams,Nelson,Baker,Hall,Rivera,Campbell,Mitchell,Carter,Roberts,Gomez,Phillips,Evans,Turner,Diaz,Parker,Cruz,Edwards,Collins,Reyes,Stewart,Morris,Morales,Murphy,Cook,Rogers,Gutierrez,Ortiz,Morgan,Cooper,Peterson,Bailey,Reed,Kelly,Howard,Ramos,Kim,Cox,Ward,Richardson,Watson,Brooks,Chavez,Wood,James,Bennett,Gray,Mendoza,Ruiz,Hughes,Price,Alvarez,Castillo,Sanders,Patel,Myers,Long,Ross,Foster,Jimenez,Powell,Jenkins,Perry,Russell,Sullivan,Bell,Coleman,Butler,Henderson,Barnes,Gonzales,Fisher,Vasquez,Simmons,Romero,Jordan,Patterson,Alexander,Hamilton,Graham,Reynolds,Griffin,Wallace,Moreno,West,Cole,Hayes,Stone,Ellis,Quinn,Harrison,Spencer,Bishop,Porter,Webb,Fox,Grant,Lawson,Marshall,Rhodes";
    private const string MaleEnglishGiven = "Aaron,Adam,Adrian,Aiden,Alan,Albert,Alexander,Andrew,Anthony,Arthur,Asher,Austin,Benjamin,Blake,Brandon,Brian,Caleb,Cameron,Carl,Charles,Christian,Christopher,Colin,Connor,Daniel,David,Dean,Declan,Dominic,Dylan,Edward,Eli,Elias,Elijah,Elliot,Ethan,Evan,Felix,Finn,Francis,Frederick,Gabriel,Gavin,George,Graham,Grant,Gregory,Harrison,Harvey,Henry,Hugo,Hunter,Isaac,Jack,Jacob,James,Jason,Jasper,Jeremy,Joel,John,Jonathan,Jordan,Joseph,Joshua,Julian,Justin,Kai,Kevin,Landon,Lawrence,Leo,Leon,Leonard,Levi,Liam,Logan,Louis,Lucas,Luke,Marcus,Mark,Mason,Matthew,Maxwell,Michael,Miles,Nathan,Nicholas,Noah,Nolan,Oliver,Oscar,Owen,Patrick,Paul,Peter,Philip,Raymond,Rhys,Robert,Roman,Ryan,Samuel,Sebastian,Simon,Spencer,Stephen,Theodore,Thomas,Timothy,Tobias,Tristan,Victor,Vincent,William,Wyatt,Zachary";
    private const string FemaleEnglishGiven = "Abigail,Ada,Adelaide,Adele,Adriana,Alice,Allison,Alyssa,Amelia,Amy,Anna,Annabelle,Annie,Arabella,Aria,Ariana,Audrey,Aurora,Ava,Beatrice,Bella,Bethany,Bianca,Bonnie,Brianna,Camila,Caroline,Cassandra,Catherine,Cecilia,Celeste,Charlotte,Chloe,Clara,Cora,Daisy,Danielle,Delilah,Diana,Eleanor,Elena,Elisa,Elizabeth,Ella,Ellie,Eloise,Elsie,Emily,Emma,Erin,Esme,Esther,Eva,Evelyn,Faith,Felicity,Fiona,Florence,Francesca,Freya,Gabriella,Gemma,Georgia,Grace,Hannah,Harriet,Hazel,Heidi,Helena,Holly,Imogen,Iris,Isabel,Isabella,Isla,Ivy,Jade,Jasmine,Jessica,Josephine,Joy,Julia,Juliet,Katherine,Keira,Laura,Lauren,Layla,Leah,Lena,Lila,Lily,Lola,Lucy,Luna,Lydia,Madeleine,Maeve,Margaret,Maria,Martha,Matilda,Maya,Megan,Melanie,Mia,Mila,Molly,Naomi,Natalie,Nora,Olivia,Ophelia,Paige,Penelope,Phoebe,Poppy,Rachel,Rebecca,Rose,Rosie,Ruby,Sabrina,Sadie,Samantha,Sarah,Scarlett,Serena,Sophia,Stella,Thea,Valerie,Vanessa,Victoria,Violet,Vivian,Willow,Zara,Zoe";
    private const string NeutralEnglishGiven = "Ash,Aspen,Avery,Bailey,Blair,Blake,Cameron,Campbell,Casey,Charlie,Corey,Dakota,Dana,Darcy,Devon,Drew,Eden,Ellery,Ellis,Emerson,Emery,Finley,Frankie,Gray,Harley,Harper,Hayden,Hollis,Indigo,Jamie,Jay,Jesse,Jordan,Jules,Kai,Kendall,Kennedy,Kit,Lane,Lennon,Leslie,Linden,Logan,London,Marley,Merritt,Milan,Monroe,Morgan,Nico,Oakley,Parker,Pat,Peyton,Phoenix,Quinn,Reagan,Reese,Remy,Rene,Riley,River,Robin,Rory,Rowan,Sage,Sam,Sasha,Sawyer,Shawn,Shea,Shiloh,Sidney,Skyler,Sloane,Stevie,Sutton,Tatum,Taylor,Terry,Toby,Tracy,Val,Wren,Winter,Alex";

    private static readonly string[][] ExpandedCache = new string[6][];

    internal static bool IsLegacyPool(IEnumerable<string> names, EmployeeGender gender, bool english) =>
        new HashSet<string>(DefaultNames(gender, english).Split(','), StringComparer.OrdinalIgnoreCase).SetEquals(names);

    internal static string[] ExpandedNames(EmployeeGender gender, bool english)
    {
        int group = gender == EmployeeGender.Male ? 0 : gender == EmployeeGender.Female ? 1 : 2;
        int index = group + (english ? 3 : 0);
        if (ExpandedCache[index] != null) return ExpandedCache[index];
        string given = english ? group == 0 ? MaleEnglishGiven : group == 1 ? FemaleEnglishGiven : NeutralEnglishGiven :
            group == 0 ? MaleChineseGiven : group == 1 ? FemaleChineseGiven : NeutralChineseGiven;
        string family = english ? EnglishSurnames : ChineseSurnames;
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in DefaultNames(gender, english).Split(','))
            if (seen.Add(name)) result.Add(name);
        foreach (string first in given.Split(','))
            foreach (string last in family.Split(','))
            {
                string name = english ? first + " " + last : last + first;
                if (name.Length <= 25 && seen.Add(name)) result.Add(name);
            }
        return ExpandedCache[index] = result.ToArray();
    }

    internal static string DefaultNames(EmployeeGender gender, bool english)
    {
        if (gender == EmployeeGender.Male) return english ? MaleEnglish : MaleChinese;
        if (gender == EmployeeGender.Female) return english ? FemaleEnglish : FemaleChinese;
        return english ? NeutralEnglish : NeutralChinese;
    }

    internal static EmployeeGender Resolve(NPC_Manager npc, int employeeIndex)
    {
        if (npc == null || npc.hiredEmployeesData == null || employeeIndex < 0 || employeeIndex >= npc.hiredEmployeesData.Length)
            return EmployeeGender.Unknown;

        string record = npc.hiredEmployeesData[employeeIndex];
        if (string.IsNullOrWhiteSpace(record)) return EmployeeGender.Unknown;
        string appearance = record.Split('|')[0];
        if (!int.TryParse(appearance, out int appearanceId) || npc.NPCsEmployeesArray == null ||
            appearanceId < 0 || appearanceId >= npc.NPCsEmployeesArray.Length)
            return EmployeeGender.Unknown;

        // NPC_Info.CreateNPCCharacter uses this same appearance ID and prefab array.
        GameObject prefab = npc.NPCsEmployeesArray[appearanceId];
        if (prefab == null) return EmployeeGender.Unknown;
        Animator animator = prefab.GetComponent<Animator>();
        if (animator != null && animator.avatar != null)
        {
            var gender = FromModelName(animator.avatar.name);
            if (gender != EmployeeGender.Unknown) return gender;
        }

        var prefabGender = FromModelName(prefab.name);
        if (prefabGender != EmployeeGender.Unknown) return prefabGender;

        EmployeeGender meshGender = EmployeeGender.Unknown;
        foreach (var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.sharedMesh == null) continue;
            var candidate = FromModelName(renderer.sharedMesh.name);
            if (candidate == EmployeeGender.Unknown) continue;
            if (meshGender != EmployeeGender.Unknown && meshGender != candidate) return EmployeeGender.Unknown;
            meshGender = candidate;
        }
        return meshGender;
    }

    private static EmployeeGender FromModelName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return EmployeeGender.Unknown;
        if (name.StartsWith("Girl_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Female_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Woman_", StringComparison.OrdinalIgnoreCase)) return EmployeeGender.Female;
        if (name.StartsWith("Man_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Male_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Boy_", StringComparison.OrdinalIgnoreCase)) return EmployeeGender.Male;
        return EmployeeGender.Unknown;
    }
}
