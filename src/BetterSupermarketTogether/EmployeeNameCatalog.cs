using System;
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
