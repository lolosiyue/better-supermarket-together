using BetterSupermarketTogether;

internal static class BondChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var r=new CompanionRelationship();
        check(r.Stage==0 && r.StageWire=="new coworker","relationship starts as new coworkers");
        check(r.Talked(1)==0 && r.Affinity==10 && r.Chats==1,"first chat of the day counts most");
        r.Talked(1);
        check(r.Affinity==12,"later chats the same day count a little");
        for(int i=0;i<100;i++)r.Talked(1);
        check(r.Affinity==60 && r.GainedToday==CompanionRelationship.DailyCap,"daily gain is capped so chat spam cannot farm affinity");
        check(r.Day(2)==0 && r.DaysTogether==1 && r.Affinity==65,"a new game day together adds a little and resets the cap");
        check(r.Day(2)==0 && r.DaysTogether==1,"the same day is not counted twice");
        check(r.Talked(2)==0 && r.Affinity==75,"first chat bonus returns on a new day");
        check(r.Gain(2,30)==1 && r.Stage==1 && r.StageWire=="familiar coworker","crossing a threshold reports one stage gained");
        r.Affinity=2000;r.GainedToday=-5;r.Chats=-1;r.LastPlayed="not a date";r.Sanitize();
        check(r.Affinity==CompanionRelationship.Max && r.GainedToday==0 && r.Chats==0 && r.LastPlayed==null && r.Stage==4,"damaged relationship values are clamped");
        var fresh=new CompanionRelationship();
        check(fresh.Arrive(new DateTime(2026,11,6))==0 && fresh.LastPlayed=="2026-11-06","first session has no absence");
        check(fresh.Arrive(new DateTime(2026,11,6,23,0,0))==0 && fresh.Arrive(new DateTime(2026,11,10))==4,"absence is counted in real days");
        check(fresh.Summary(false).Contains("新同事") && fresh.Summary(true).Contains("New coworker"),"relationship summary follows the UI language");

        var d=new CompanionDiary();
        check(d.Note(3,"A") && !d.Note(3,"A") && d.Note(3,"B"),"consecutive duplicate notes are skipped");
        for(int i=0;i<20;i++)d.Note(3,"note "+i);
        check(d.Notes.Count==CompanionDiary.MaxNotes && d.Notes.Last()=="note 19","notes keep the latest dozen");
        check(!d.Note(4,"tomorrow") && d.NotesDay==3,"a new day waits for the previous diary to be written");
        check(!d.Due(3,false) && d.Due(3,true) && d.Due(4,false) && d.Due(2,false),"diary is due at closing or on any other day");
        var notes=d.Take(out int day);
        check(day==3 && notes.Count==12 && d.Notes.Count==0 && !d.Due(4,true),"taking notes clears the pending day");
        d.Restore(day,notes);
        check(d.NotesDay==3 && d.Notes.Count==12,"failed writes restore the notes");
        d.Take(out _);d.Note(4,"new");d.Restore(3,notes);
        check(d.NotesDay==4 && d.Notes.Single()=="new","restore never overwrites a newer day");
        d.Add(5,"2026-11-06","「今天好忙。」\n");d.Add(4,"2026-11-05","昨天");d.Add(5,"2026-11-06","改寫");
        check(d.Entries.Count==2 && d.Entries[0].Day==4 && d.Entries[1].Text=="改寫","entries are cleaned, sorted and one per day");
        var snap=d.Snapshot(7);
        check((int?)snap["recent_days"]![1]!["days_ago"]==2 && snap["long_ago"]!.Type==Newtonsoft.Json.Linq.JTokenType.Null,"shared memories report how long ago");
        for(int i=6;i<=20;i++)d.Add(i,"x","day "+i);
        check(d.NeedsRollUp && d.Oldest.Count==CompanionDiary.RollCount && d.Oldest[0].Day==4,"more than two weeks of entries need condensing");
        var oldest=d.Oldest;d.RolledUp(oldest,"");
        check(d.Entries.Count==17,"an empty summary keeps the entries");
        d.RolledUp(oldest,"那兩週一起熬過很多忙碌的日子。");
        check(d.Entries.Count==10 && d.LongAgo!=null && !d.NeedsRollUp && d.Entries[0].Day==11,"condensing replaces the oldest week with one memory");
        check(d.Summary(false).StartsWith("日記") && d.Summary(true).Contains("Day 20"),"diary summary shows the latest entries");

        string path=Path.Combine(Path.GetTempPath(),"bst-bond-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            var memory=new CompanionMemory();memory.Relationship.Talked(1);memory.Diary.Note(1,"記住這一天");memory.Diary.Add(1,"2026-11-06","第一天");
            memory.Save(path);var loaded=CompanionMemory.Load(path);
            check(loaded.Relationship.Affinity==10 && loaded.Diary.Notes.Single()=="記住這一天" && loaded.Diary.Entries.Single().Text=="第一天","relationship and diary survive save and load");
            File.WriteAllText(path,"{\"Version\":1,\"Facts\":[],\"History\":[]}");
            loaded=CompanionMemory.Load(path);
            check(loaded.Relationship!=null && loaded.Relationship.Affinity==0 && loaded.Diary!=null && loaded.Diary.Entries.Count==0,"older memory files load with an empty relationship and diary");
            File.WriteAllText(path,"{\"Version\":1,\"Facts\":[],\"History\":[],\"Relationship\":{\"Affinity\":99999},\"Diary\":{\"Entries\":[null,{\"Day\":1,\"Text\":\"\"}],\"Notes\":null}}");
            loaded=CompanionMemory.Load(path);
            check(loaded.Relationship.Affinity==CompanionRelationship.Max && loaded.Diary.Entries.Count==0 && loaded.Diary.Notes!=null,"damaged relationship and diary data are repaired on load");
        }
        finally { foreach(var file in new[]{path,path+".bak",path+".tmp"})if(File.Exists(file))File.Delete(file); }
    }
}
