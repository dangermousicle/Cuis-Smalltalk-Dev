'From Squeak3.7 of ''4 September 2004'' [latest update: #5989] on 18 November 2008 at 1:05:58 pm'!
		stop: contents size
		internalSpaces: 0
		paddingWidth: 0.
		rectangle: bounds;
		lineHeight: f height baseline: f ascent.
	"Install an editor for my contents.  This constitutes 'hasFocus'.
	If priorEditor is not nil, then initialize the new editor from its state.
	We may want to rework this so it actually uses the prior editor."

	| stateArray |
	priorEditor ifNotNil: [stateArray := priorEditor stateArray].
	editor := SimpleEditor new morph: self.
	editor changeString: contents.
	priorEditor ifNotNil: [editor stateArrayPut: stateArray].
	self changed.
	^editor! !
	"Perform the changes in interactionBlock, noting any change in selection
	and possibly a change in the size of the paragraph (ar 9/22/2001 - added for TextPrintIts)"

	"Also couple the editor to Morphic keyboard events"

	| oldEditor oldContents |
	self editor sensor: (OldKeyboardBuffer new startingEvent: evt).
	oldEditor := editor.
	oldContents := contents.
	interactionBlock value.
	oldContents == contents 
		ifTrue: 
			["this will not work if the paragraph changed"

			editor := oldEditor	"since it may have been changed while in block"].