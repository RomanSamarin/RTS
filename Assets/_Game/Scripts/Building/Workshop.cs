using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

[RequireComponent(typeof(WorkplaceHiring))]
public class Workshop : EquipmentProductionBuilding
{
    public GameObject catapultPrefab;
    public Transform catapultSpawnPoint;
    public Sprite catapultIcon;
    private bool blockedExit;
    private float nextSpawnAttempt;
    public bool WaitingForCatapultExit=>blockedExit&&ActiveRecipe!=null&&ActiveRecipe.producesUnit&&WorkProgress>=ActiveRecipe.seconds;
    private static Recipe CatapultRecipe()=>new Recipe{title="Катапульта",producesUnit=true,seconds=90,quantity=1,
        ingredients=new List<Stock>{new Stock(ResourceType.Wood,30),new Stock(ResourceType.Iron,10),new Stock(ResourceType.Stone,5)}};
    protected override void PrepareRecipes(List<Recipe> values)
    {if(!values.Exists(recipe=>recipe!=null&&recipe.producesUnit))values.Add(CatapultRecipe());}
    protected override bool CanAcceptOutput(Recipe recipe)=>recipe.producesUnit?recipe.quantity==1&&CatapultFactory.Valid(catapultPrefab):base.CanAcceptOutput(recipe);
    protected override bool TryFinishOutput(Recipe recipe)
    {
        if(!recipe.producesUnit)return base.TryFinishOutput(recipe);
        if(Time.time<nextSpawnAttempt)return false;
        nextSpawnAttempt=Time.time+1;
        blockedExit=!CatapultFactory.TrySpawn(catapultPrefab,gameObject,catapultSpawnPoint);
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
    {for(int i=0;i<Recipes.Count;i++)if(Recipes[i].producesUnit){QueueRecipe(i);return;}}
}
