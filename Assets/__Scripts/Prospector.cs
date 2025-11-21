using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;   // We’ll need this line later in the chapter

[RequireComponent(typeof(Deck))]                                              // a
[RequireComponent(typeof(JsonParseLayout))]
public class Prospector : MonoBehaviour
{
    private static Prospector S; // A private Singleton for Prospector

    [Header("Dynamic")]
    public List<CardProspector> drawPile;

    public List<CardProspector> discardPile;
    public List<CardProspector> mine;
    public CardProspector target;

    private Transform layoutAnchor;

    private Deck deck;
    private JsonLayout jsonLayout;
    public CardProspector selectedCard;


    // A Dictionary to pair mine layout IDs and actual Cards
    private Dictionary<int, CardProspector> mineIdToCardDict;                 // a


    void Start()
    {
        // Set the private Singleton. We’ll use this later.
        if (S != null) Debug.LogError("Attempted to set S more than once!");  // b
        S = this;

        jsonLayout = GetComponent<JsonParseLayout>().layout;

        deck = GetComponent<Deck>();
        // These two lines replace the Start() call we commented out in Deck
        deck.InitDeck();
        Deck.Shuffle(ref deck.cards);

        drawPile = ConvertCardsToCardProspectors(deck.cards);

        LayoutMine();
        selectedCard = Draw();
        MoveToTarget(selectedCard);
        UpdateDrawPile();
    }

    /// <summary>
    /// Converts each Card in a List(Card) into a List(CardProspector) so that it
    ///  can be used in the Prospector game.
    /// </summary>
    /// <param name="listCard">A List(Card) to be converted</param>
    /// <returns>A List(CardProspector) of the converted cards</returns>
    List<CardProspector> ConvertCardsToCardProspectors(List<Card> listCard)
    {
        List<CardProspector> listCP = new List<CardProspector>();
        CardProspector cp;
        foreach (Card card in listCard)
        {
            cp = card as CardProspector;                                      // c
            listCP.Add(cp);
        }
        return (listCP);
    }

    /// <summary>
    /// Pulls a single card from the beginning of the drawPile and returns it
    /// Note: There is no protection against trying to draw from an empty pile!
    /// </summary>
    /// <returns>The top card of drawPile</returns>
    CardProspector Draw()
    {
        CardProspector cp = drawPile[0]; // Pull the 0th CardProspector
        drawPile.RemoveAt(0);            // Then remove it from drawPile
        return (cp);                      // And return it
    }

    /// <summary>
    /// Positions the initial tableau of cards, a.k.a. the "mine"
    /// </summary>
    void LayoutMine()
    {
        // Create an empty GameObject to serve as an anchor for the tableau   // a
        if (layoutAnchor == null)
        {
            // Create an empty GameObject named _LayoutAnchor in the Hierarchy
            GameObject tGO = new GameObject("_LayoutAnchor");
            layoutAnchor = tGO.transform;             // Grab its Transform
        }

        CardProspector cp;

        // Generate the Dictionary to match mine layout ID to CardProspector
        mineIdToCardDict = new Dictionary<int, CardProspector>();             // b


        // Iterate through the JsonLayoutSlots pulled from the JSON_Layout
        foreach (JsonLayoutSlot slot in jsonLayout.slots)
        {
            cp = Draw(); // Pull a card from the top (beginning) of the draw Pile
            cp.faceUp = slot.faceUp;    // Set its faceUp to the value in SlotDef
                                        // Make the CardProspector a child of layoutAnchor
            cp.transform.SetParent(layoutAnchor);

            // Convert the last char of the layer string to an int (e.g. "Row 0")
            int z = int.Parse(slot.layer[slot.layer.Length - 1].ToString());  // c

            // Set the localPosition of the card based on the slot information
            cp.SetLocalPos(new Vector3(
            jsonLayout.multiplier.x * slot.x,
            jsonLayout.multiplier.y * slot.y,
            -z));                                                       // d

            cp.layoutID = slot.id;
            cp.layoutSlot = slot;
            // CardProspectors in the mine have the state CardState.mine
            cp.state = eCardState.mine;

            // Set the sorting layer of all SpriteRenderers on the Card
            cp.SetSpriteSortingLayer(slot.layer);

            mine.Add(cp); // Add this CardProspector to the List<mine>

            // Add this CardProspector to the mineIDtoCardDict Dictionary
            mineIdToCardDict.Add(slot.id, cp);                                // c

        }
    }

    /// <summary>
    /// Moves the current target card to the discardPile
    /// </summary>
    /// <param name="cp">The CardProspector to be moved</param>
    void MoveToDiscard(CardProspector cp)
    {
        // Set the state of the card to discard
        cp.state = eCardState.discard;
        discardPile.Add(cp);  // Add it to the discardPile List<>
        cp.transform.SetParent(layoutAnchor); // Update its transform parent

        // Position it on the discardPile
        cp.SetLocalPos(new Vector3(
        jsonLayout.multiplier.x * jsonLayout.discardPile.x,
        jsonLayout.multiplier.y * jsonLayout.discardPile.y,
        0));

        cp.faceUp = true;

        // Place it on top of the pile for depth sorting
        cp.SetSpriteSortingLayer(jsonLayout.discardPile.layer);               // a
        cp.SetSortingOrder(-200 + (discardPile.Count * 3));                  // b
        Collider2D col = cp.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
    }

    /// <summary>
    /// Make cp the new target card
    /// </summary>
    /// <param name="cp">The CardProspector to be moved</param>
    void MoveToTarget(CardProspector cp)
    {
        // If there is currently a target card, move it to discardPile
        if (target != null) MoveToDiscard(target);
        // Set the new target
        target = cp;
        cp.state = eCardState.target;

        // Make sure it is face-up
        cp.faceUp = true;
        // Use MoveToDiscard to move the target card to the correct location
        cp.SetLocalPos(new Vector3(
        jsonLayout.multiplier.x * jsonLayout.discardPile.x,
        jsonLayout.multiplier.y * jsonLayout.discardPile.y,
        0));                                                  // c

        // Then set a few additional things to make cp the new target
        target = cp; // cp is the new target
        cp.state = eCardState.target;

        // Set the depth sorting so that cp is on top of the discardPile
        cp.SetSpriteSortingLayer("Target");                                 // c
        cp.SetSortingOrder(0);
    }

    /// <summary>
    /// Arranges all the cards of the drawPile to show how many are left
    /// </summary>
    void UpdateDrawPile()
    {
        CardProspector cp;
        // Go through all the cards of the drawPile
        for (int i = 0; i < drawPile.Count; i++)
        {
            cp = drawPile[i];
            cp.transform.SetParent(layoutAnchor);

            // Position it correctly with the layout.drawPile.stagger
            Vector3 cpPos = new Vector3();
            cpPos.x = jsonLayout.multiplier.x * jsonLayout.drawPile.x;
            // Add the staggering for the drawPile
            cpPos.x += jsonLayout.drawPile.xStagger * i;
            cpPos.y = jsonLayout.multiplier.y * jsonLayout.drawPile.y;
            cpPos.z = 0.1f * i;
            cp.SetLocalPos(cpPos);

            cp.faceUp = false; // DrawPile Cards are all face-down
            cp.state = eCardState.drawpile;
            // Set depth sorting
            cp.SetSpriteSortingLayer(jsonLayout.drawPile.layer);
            cp.SetSortingOrder(-10 * i);
        }
    }

    /// <summary>
    /// This turns cards in the Mine face-up and face-down
    /// </summary>
    public void SetMineFaceUps()
    {                                            // d
        CardProspector coverCP;
        foreach (CardProspector cp in mine)
        {
            bool faceUp = true; // Assume the card will be face-up

            // Iterate through the covering cards by mine layout ID
            foreach (int coverID in cp.layoutSlot.hiddenBy)
            {
                coverCP = mineIdToCardDict[coverID];
                // If the covering card is null or still in the mine...
                if (coverCP == null || coverCP.state == eCardState.mine)
                {
                    faceUp = false; // then this card is face-down
                }
            }
            cp.faceUp = faceUp; // Set the value on the card
        }
    }

    bool IsBlocked(CardProspector cp)
    {
        foreach (int coverID in cp.layoutSlot.hiddenBy)
        {
            CardProspector coverCP = mineIdToCardDict[coverID];

            if (coverCP != null && coverCP.state == eCardState.mine)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Handler for any time a card in the game is clicked
    /// </summary>
    /// <param name="cp">The CardProspector that was clicked</param>
    static public void CARD_CLICKED(CardProspector cp)
    {   Debug.Log($"Clicked Card -> ID: {cp.layoutID}, Rank: {cp.rank}, selectedcard: {(S.selectedCard != null ? S.selectedCard.rank.ToString() : "null")}");

        // The reaction is determined by the state of the clicked card
        switch (cp.state)
        {
            
            
            case eCardState.target:
                // Clicking the target card does nothing
                break;
            case eCardState.drawpile:
                // Clicking *any* card in the drawPile will draw the next card
                // Call two methods on the Prospector Singleton S
                CardProspector newTarget = S.Draw();
                S.MoveToDiscard(S.selectedCard);
                S.selectedCard = newTarget;
                S.MoveToTarget(newTarget);  // Draw a new target card
                S.UpdateDrawPile();          // Restack the drawPile
                break;
            case eCardState.mine:
                if(S.IsBlocked(cp)){ 
                 break;
                }
                // Clicking a card in the mine will check if it’s a valid play
                if(cp.rank == 13){
                    S.mine.Remove(cp);
                    S.MoveToDiscard(cp);
                    S.target = null;
                    S.SetMineFaceUps();  // Be sure to add this line!!
                    break;
                }

                if(S.target != null && S.target.state == eCardState.mine){
                    Debug.Log($"Attempting Pair -> Target Rank: {S.target.rank}, Clicked Rank: {cp.rank}");
                    if (cp.rank + S.target.rank == 13){
                        S.mine.Remove(cp);


                        S.MoveToDiscard(cp);
                        if (S.target.state == eCardState.mine) S.mine.Remove(S.target);
                        S.MoveToDiscard(S.target);

                        S.target = null;
                        S.SetMineFaceUps();
                        Debug.Log($"worked Target Rank: {(S.target != null ? S.target.rank.ToString() : "null")}, cp Clicked Rank: {cp.rank}");

                        break;
                    }
                    Debug.Log($"Attempting Pair -> Target Rank: {S.target.rank}, Clicked Rank: {S.selectedCard.rank}");
                    if (S.target.rank + S.selectedCard.rank == 13){

                        CardProspector oldSelected = S.selectedCard;

                        // Remove both from mine
                        S.mine.Remove(S.target);
                        S.mine.Remove(oldSelected);

                        // Move both to discard
                        S.MoveToDiscard(S.target);
                        S.MoveToDiscard(oldSelected);

                        // Make old selected invisible & inactive
                        oldSelected.SetSpriteSortingLayer("Row6");
                        oldSelected.SetSortingOrder(-999);
                        Collider2D col = oldSelected.GetComponent<Collider2D>();
                        if (col != null) col.enabled = false;

                        // Clear target
                        S.target = null;

                        // Draw a new selected card
                        S.selectedCard = S.Draw();
                        S.MoveToTarget(S.selectedCard);
                        S.selectedCard.SetSpriteSortingLayer("NewDrawn");
                        S.selectedCard.SetSortingOrder(999);
                        Debug.Log($"Drew a {S.selectedCard.rank}");

                        S.UpdateDrawPile();
                        S.SetMineFaceUps();

                        Debug.Log(
                         $"Attempting Pair -> Target Rank: NULL, Selected Rank: {S.selectedCard.rank}");

                        break;
                    }
                    else{
                        S.target = cp;
                    }
                }
                else {
                    S.target = cp;
                }
                break;
        }
    }

}