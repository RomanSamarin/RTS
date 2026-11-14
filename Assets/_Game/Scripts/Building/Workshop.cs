using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

[RequireComponent(typeof(WorkplaceHiring))]
public class Workshop : EquipmentProductionBuilding
{
    public GameObject catapultPrefab;
    public Transform catapultSpawnPoint;
    public Sprite catapultIcon;
    public GameObject ladderCartPrefab;
    public Transform ladderCartSpawnPoint;
    public Sprite ladderCartIcon;
    public GameObject ramPrefab;
    public Transform ramSpawnPoint;
    public Sprite ramIcon;
    private bool blockedExit;
    private float nextSpawnAttempt;
    public bool WaitingForCatapultExit=>blockedExit&&IsCatapult(ActiveRecipe)&&WorkProgress>=ActiveRecipe.seconds;
    public bool WaitingForRamExit=>blockedExit&&ActiveRecipe!=null&&ActiveRecipe.producesRam&&WorkProgress>=ActiveRecipe.seconds;
    public bool WaitingForLadderCartExit=>blockedExit&&ActiveRecipe!=null&&ActiveRecipe.producesUnit&&ActiveRecipe.producesLadderCart&&WorkProgress>=ActiveRecipe.seconds;
    private static Recipe CatapultRecipe()=>new Recipe{title="Катапульта",producesUnit=true,seconds=90,quantity=1,
        ingredients=new List<Stock>{new Stock(ResourceType.Wood,30),new Stock(ResourceType.Iron,10),new Stock(ResourceType.Stone,5)}};
    private static Recipe LadderRecipe() => new Recipe
    {
        title = "Осадная лестница", output = ResourceType.SiegeLadder, seconds = 35, quantity = 1,
        ingredients = new List<Stock> { new Stock(ResourceType.Wood, 8) }
    };
    private static Recipe LadderCartRecipe() => new Recipe
    {
        title = "Осадная повозка", producesUnit = true, producesLadderCart = true, seconds = 45,
        ingredients = new List<Stock> { new Stock(ResourceType.Wood, 20), new Stock(ResourceType.Iron, 4) }
    };
    private static bool IsCatapult(Recipe recipe)
        => recipe != null && recipe.producesUnit && !recipe.producesLadderCart && !recipe.producesRam;
    protected override void PrepareRecipes(List<Recipe> values)
    {
        // Append only: existing queues and manually configured UI refer to recipe indices.
        if (!values.Exists(IsCatapult)) values.Add(CatapultRecipe());
        if (!values.Exists(recipe => recipe != null && !recipe.producesUnit && recipe.output == ResourceType.SiegeLadder))
            values.Add(LadderRecipe());
        if (!values.Exists(recipe => recipe != null && recipe.producesUnit && recipe.producesLadderCart)) values.Add(LadderCartRecipe());
        if (!values.Exists(recipe => recipe != null && recipe.producesUnit && recipe.producesRam))
            values.Add(new Recipe
            {
                title = "Таран", producesUnit = true, producesRam = true, seconds = 60,
                ingredients = new List<Stock> { new Stock(ResourceType.Wood, 25), new Stock(ResourceType.Iron, 6) }
            });
    }
    public int SiegeLadderRecipeIndex
    {
        get
        {
            var values = Recipes;
            for (int i = 0; i < values.Count; i++)
                if (values[i] != null && !values[i].producesUnit && values[i].output == ResourceType.SiegeLadder) return i;
            return -1;
        }
    }
    public void SelectSiegeLadder() { SetRecipe(SiegeLadderRecipeIndex); }
    public void QueueSiegeLadder() { QueueRecipe(SiegeLadderRecipeIndex); }
    public void QueueLadderCart()
    { for (int i = 0; i < Recipes.Count; i++) if (Recipes[i].producesUnit && Recipes[i].producesLadderCart) { QueueRecipe(i); return; } }
    protected override bool CanAcceptOutput(Recipe recipe)=>recipe.producesUnit?recipe.quantity==1
        &&(recipe.producesRam?RamFactory.Valid(ramPrefab):recipe.producesLadderCart?SiegeLadderCartFactory.Valid(ladderCartPrefab):CatapultFactory.Valid(catapultPrefab)):base.CanAcceptOutput(recipe);
    protected override bool TryFinishOutput(Recipe recipe)
    {
        if(!recipe.producesUnit)return base.TryFinishOutput(recipe);
        if(Time.time<nextSpawnAttempt)return false;
        nextSpawnAttempt=Time.time+1;
        blockedExit=!(recipe.producesRam ? RamFactory.TrySpawn(ramPrefab, gameObject, ramSpawnPoint) : recipe.producesLadderCart
            ?SiegeLadderCartFactory.TrySpawn(ladderCartPrefab,gameObject,ladderCartSpawnPoint)
            :CatapultFactory.TrySpawn(catapultPrefab,gameObject,catapultSpawnPoint));
        return !blockedExit;
    }
    public override string WaitingTask=>ActiveRecipe!=null&&ActiveRecipe.producesUnit&&blockedExit?"Выход мастерской занят":base.WaitingTask;
    public override ResourcesMove.UnitRole Profession => ResourcesMove.UnitRole.Craftsman;
    protected override List<Recipe> DefaultRecipes() => new List<Recipe>
    {
        new Recipe { title = "Лук", output = ResourceType.Bow, seconds = 25,
            ingredients = new List<Stock> { new Stock(ResourceType.Wood, 3) } },
        new Recipe { title = "Щит", output = ResourceType.Shield, seconds = 20,
            ingredients = new List<Stock> { new Stock(ResourceType.Wood, 2) } }
    };
    public void SelectBow() { SetRecipe(0); }
    public void SelectShield() { SetRecipe(1); }
    public void QueueBow() { QueueRecipe(0); }
    public void QueueShield() { QueueRecipe(1); }
    public void QueueCatapult()
    {for(int i=0;i<Recipes.Count;i++)if(IsCatapult(Recipes[i])){QueueRecipe(i);return;}}
    public void QueueRam()
    {for(int i=0;i<Recipes.Count;i++)if(Recipes[i].producesRam){QueueRecipe(i);return;}}
}
