using TennoForge.Core;

internal static class MasteryMaterialChecks
{
    internal static void Run(Action<string,Action> check)
    {
        static void Yes(bool value){if(!value)throw new Exception("마스터리 재료 합산 불일치");}
        static InventoryItem Item(string id,bool masterable=false,int rank=30)=>new(id,id,id,id=="f"?"Resources":masterable?"Primary":"Parts",masterable,100,rank,0,false,false,null,null);
        static InventoryRecipe Recipe(string id,int quantity,params RecipeIngredient[] ingredients)=>new(id+"-bp",id,100,60,quantity,true,ingredients);
        var catalog=new InventoryCatalog(1,DateTimeOffset.UtcNow,[Item("a",true),Item("b",true),Item("c",true),Item("f")],
            [Recipe("a",1,new RecipeIngredient("f",2)),Recipe("b",1,new RecipeIngredient("a",2),new RecipeIngredient("f",3)),Recipe("c",1,new RecipeIngredient("f",4))],[]);
        var snapshot=InventorySnapshot.Empty with{MasteryComplete=true,InventoryComplete=true};
        check("전체 제작 재료: 선행 무기를 랭작 후 소모하며 중복 제작 제외",()=>
        {var r=MasteryMaterials.Calculate(catalog,snapshot);Yes(r.RemainingCraftable==3&&r.CraftJobs==4&&r.Materials.Single().Required==11);});
        check("전체 제작 재료: 공유 재고는 한 번만 차감·부족분 계산",()=>
        {var r=MasteryMaterials.Calculate(catalog,snapshot with{Quantities=new(){["f"]=7}});Yes(r.Materials.Single().Required==11&&r.Materials.Single().Missing==4);});
        check("전체 제작 재료: 숙련 완료 제외·제작 중 비용 중복 합산 금지",()=>
        {var r=MasteryMaterials.Calculate(catalog,snapshot with{MasteryRanks=new(){["a"]=30,["b"]=30},Crafting=[new("c-bp",null)]});Yes(r.RemainingCraftable==1&&r.AlreadyAvailable==1&&r.Materials.Length==0&&r.Credits==0);});
        check("전체 제작 재료: 보유 장비 소모는 명시적 선택에서만 재료 절약",()=>
        {var s=snapshot with{Quantities=new(){["a"]=1}};Yes(MasteryMaterials.Calculate(catalog,s).Materials.Single().Required==11&&MasteryMaterials.Calculate(catalog,s,true).Materials.Single().Required==9);});
        check("전체 제작 재료: 직접 구매 선행 장비도 보존 설정을 지킴",()=>
        {
            var c=catalog with{Recipes=catalog.Recipes.Where(r=>r.ResultId!="a").ToArray()};var s=snapshot with{Quantities=new(){["a"]=1}};
            var row=MasteryMaterials.Calculate(c,s).Materials.Single(r=>r.Id=="a");Yes(row.Owned==1&&row.Missing==2&&row.ProtectedStock==1);
            Yes(MasteryMaterials.Calculate(c,s,true).Materials.Single(r=>r.Id=="a").Missing==1);
        });
        check("전체 제작 재료: 일괄 제작 산출 수량·부품 재고·반복 설계도",()=>
        {
            var c=new InventoryCatalog(1,DateTimeOffset.UtcNow,[Item("a",true),Item("b",true),Item("p"),Item("f")],
                [Recipe("a",1,new RecipeIngredient("p",1)),Recipe("b",1,new RecipeIngredient("p",1)),Recipe("p",3,new RecipeIngredient("f",5)) with{ConsumeBlueprint=false}],[]);
            var r=MasteryMaterials.Calculate(c,snapshot);Yes(r.Materials.Single().Required==5&&r.Blueprints.Single(x=>x.Id=="p-bp").Required==1);
            r=MasteryMaterials.Calculate(c,snapshot with{Quantities=new(){["p"]=1}});Yes(r.Materials.Single().Required==5);
        });
        check("전체 제작 재료: 이력 미확인은 미숙련 전체로 간주하지 않음",()=>Yes(!MasteryMaterials.Calculate(catalog,InventorySnapshot.Empty).MasteryKnown));
        check("전체 제작 재료: 제작비와 본체 설계도 구매 비용을 따로 합산",()=>
        {
            var c=catalog with{Items=catalog.Items.Select(i=>i.Id=="b"?i with{BlueprintCredits=500}:i).ToArray()};
            var r=MasteryMaterials.Calculate(c,snapshot);Yes(r.Credits==400&&r.BlueprintCredits==500&&r.TotalCredits==900);
        });
        check("전체 제작 재료: 필드론 등 완제품 재료를 원재료로 몰래 분해하지 않음",()=>
        {
            var c=catalog with{Recipes=catalog.Recipes.Append(Recipe("f",1,new RecipeIngredient("other",10))).ToArray()};
            Yes(MasteryMaterials.Calculate(c,snapshot).Materials.Single().Id=="f");
        });
        check("전체 제작 재료: 선택적 희귀 자원 제작식 대신 완성 자원 수량 유지",()=>
        {
            const string cell="/Lotus/Types/Items/MiscItems/OrokinCell";
            var c=catalog with{Items=[Item("a",true),Item(cell) with{Category="Misc",EnglishName="Orokin Cell"},Item("f")],
                Recipes=[Recipe("a",1,new RecipeIngredient(cell,3)),Recipe(cell,1,new RecipeIngredient("f",50000))]};
            var r=MasteryMaterials.Calculate(c,snapshot with{Quantities=new(){[cell]=1}});
            Yes(r.Materials.Length==1&&r.Materials[0].Id==cell&&r.Materials[0].Required==3&&r.Materials[0].Missing==2&&r.CraftJobs==1);
        });
        check("전체 제작 재료: Resources로 분류된 이쿼녹스·네크라메크 부품도 부족분만 제작",()=>
        {
            foreach(string part in new[]{"/Lotus/Types/Recipes/WarframeRecipes/AnimusChassisComponent","/Lotus/Types/Gameplay/InfestedMicroplanet/Resources/Mechs/NecromechPartChassisItem"})
            {
                var c=catalog with{Items=[Item("a",true),Item(part) with{Category="Resources"},Item("f")],
                    Recipes=[Recipe("a",1,new RecipeIngredient(part,2)),Recipe(part,1,new RecipeIngredient("f",7))]};
                var r=MasteryMaterials.Calculate(c,snapshot with{Quantities=new(){[part]=1}});
                Yes(r.Materials.Single().Id=="f"&&r.Materials.Single().Required==7&&r.CraftJobs==2);
                Yes(MasteryMaterials.Calculate(c,snapshot with{Quantities=new(){[part]=2}}).Materials.Length==0);
            }
        });
        check("전체 제작 재료: 희귀·가공 재료 우선 정렬은 수량·모든 재료를 보존",()=>
        {
            var c=InventoryCatalog.Load();var rows=MasteryMaterials.Calculate(c,snapshot).Materials;
            Yes(rows.Any(r=>r.EnglishName=="Orokin Cell")&&rows.Any(r=>r.EnglishName=="Neural Sensors")&&rows.Any(r=>r.EnglishName=="Neurodes"));
            int Index(string name)=>Array.FindIndex(rows,r=>r.EnglishName==name);
            Yes(Index("Fieldron")<Index("Ferrite")&&Index("Orokin Cell")<Index("Ferrite"));
            foreach(var order in Enum.GetValues<MaterialOrder>())
            {var sorted=MaterialPreparation.Sort(rows,order);Yes(sorted.Length==rows.Length&&sorted.ToHashSet().SetEquals(rows));}
            var missing=MaterialPreparation.Sort(rows,MaterialOrder.Missing);Yes(missing.Zip(missing.Skip(1)).All(p=>p.First.Missing>=p.Second.Missing));
        });
        check("전체 제작 재료: 분류는 현지화 이름이 아닌 내부 ID·가공 여부 기준",()=>
        {
            var c=InventoryCatalog.Load();var a=c.Items.Single(i=>i.Id=="/Lotus/Types/Items/Gems/Eidolon/EidolonGemACutAItem");
            Yes(MaterialPreparation.For(a,true).Group=="오픈월드 가공 재료");
            Yes(MaterialPreparation.For(a with{Name="renamed",EnglishName="renamed"},true)==MaterialPreparation.For(a,true));
            var raw=c.Items.Single(i=>i.Id=="/Lotus/Types/Items/Gems/Eidolon/CommonOreAItem");
            Yes(MaterialPreparation.For(raw,false).Group=="일반 재화");
        });
        check("전체 제작 재료: 전체 자료 계산·숙련 완료 전체 제외",()=>
        {
            var c=InventoryCatalog.Load();var r=MasteryMaterials.Calculate(c,snapshot);
            Yes(r.RemainingCraftable>100&&new[]{"Fieldron","Mutagen Mass","Detonite Injector"}.All(n=>r.Materials.Any(m=>m.EnglishName==n&&m.Required>0)));
            var all=snapshot with{MasteryRanks=c.Items.Where(i=>i.Masterable).ToDictionary(i=>i.Id,i=>i.MaxRank)};
            Yes(MasteryMaterials.Calculate(c,all).Materials.Length==0);
        });
    }
}
