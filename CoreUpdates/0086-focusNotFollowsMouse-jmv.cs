'From Squeak3.7 of ''4 September 2004'' [latest update: #5989] on 7 November 2008 at 3:25:52 pm'!
	"This message is sent to a dropped morph after it has been dropped on -- and been accepted by -- a drop-sensitive morph"

	self formerOwner: nil.
	self formerPosition: nil.
	"Note an unhappy inefficiency here:  the startStepping... call will often have already been called in the sequence leading up to entry to this method, but unfortunately the isPartsDonor: call often will not have already happened, with the result that the startStepping... call will not have resulted in the startage of the steppage."! !
	"System level event handling."
	anEvent wasHandled ifTrue:[^self]. "not interested"
	anEvent hand removePendingBalloonFor: self.
	anEvent wasHandled: true.

	(anEvent controlKeyPressed
			and: [Preferences cmdGesturesEnabled])
		ifTrue: [^ self invokeMetaMenu: anEvent].

	"Make me modal during mouse transitions"
	anEvent hand newMouseFocus: self event: anEvent.
	anEvent blueButtonChanged ifTrue:[^self blueButtonDown: anEvent].

	self mouseDown: anEvent.
	anEvent hand removeHaloFromClick: anEvent on: self.

	(self handlesMouseStillDown: anEvent) ifTrue:[
		self startStepping: #handleMouseStillDown: 
			at: Time millisecondClockValue + self mouseStillDownThreshold
			arguments: {anEvent copy resetHandlerFields}
			stepTime: self mouseStillDownStepRate ].
! !
	"The mouse has crossed a secondary (fixed-height) pane divider.  Spawn a reframe handle."

	"Only supports vertical adjustments."

	| siblings topAdjustees bottomAdjustees topOnly bottomOnly resizer pt delta minY maxY cursor |
	owner ifNil: [^self	"Spurious mouseLeave due to delete"].
	self isCollapsed ifTrue: [^self].
	((self world ifNil: [^self]) firstSubmorph isKindOf: OldNewHandleMorph) 
		ifTrue: [^self	"Prevent multiple handles"].
	divider layoutFrame ifNil: [^self].
	(#(#top #bottom) includes: divider resizingEdge) ifFalse: [^self].
	siblings := divider owner submorphs select: [:m | m layoutFrame notNil].
	divider resizingEdge = #bottom 
		ifTrue: 
			[cursor := Cursor resizeTop.
			topAdjustees := siblings select: 
							[:m | 
							m layoutFrame topFraction = divider layoutFrame bottomFraction 
								and: [m layoutFrame topOffset >= divider layoutFrame topOffset]].
			bottomAdjustees := siblings select: 
							[:m | 
							m layoutFrame bottomFraction = divider layoutFrame topFraction 
								and: [m layoutFrame bottomOffset >= divider layoutFrame topOffset]]].
	divider resizingEdge = #top 
		ifTrue: 
			[cursor := Cursor resizeBottom.
			topAdjustees := siblings select: 
							[:m | 
							m layoutFrame topFraction = divider layoutFrame bottomFraction 
								and: [m layoutFrame topOffset <= divider layoutFrame bottomOffset]].
			bottomAdjustees := siblings select: 
							[:m | 
							m layoutFrame bottomFraction = divider layoutFrame topFraction 
								and: [m layoutFrame bottomOffset <= divider layoutFrame bottomOffset]]].
	topOnly := topAdjustees copyWithoutAll: bottomAdjustees.
	bottomOnly := bottomAdjustees copyWithoutAll: topAdjustees.
	(topOnly isEmpty or: [bottomOnly isEmpty]) ifTrue: [^self].
	minY := bottomOnly inject: -9999
				into: [:y :m | y max: m top + (m minHeight max: 16) + (divider bottom - m bottom)].
	maxY := topOnly inject: 9999
				into: [:y :m | y min: m bottom - (m minHeight max: 16) - (m top - divider top)].
	pt := event cursorPoint.
	resizer := OldNewHandleMorph new 
				followHand: event hand
				forEachPointDo: 
					[:p | 
					delta := (p y min: maxY max: minY) - pt y.
					topAdjustees 
						do: [:m | m layoutFrame topOffset: m layoutFrame topOffset + delta].
					bottomAdjustees 
						do: [:m | m layoutFrame bottomOffset: m layoutFrame bottomOffset + delta].
					divider layoutChanged.
					pt := pt + delta]
				lastPointDo: [:p | ]
				withCursor: cursor.
	event hand world addMorphInLayer: resizer.
	resizer startStepping! !
	"The mouse has crossed a pane border.  Spawn a reframe handle."

	| resizer localPt pt ptName newBounds cursor |
	owner ifNil: [^self	"Spurious mouseLeave due to delete"].
	self isCollapsed ifTrue: [^self].
	((self world ifNil: [^self]) firstSubmorph isKindOf: OldNewHandleMorph) 
		ifTrue: [^self	"Prevent multiple handles"].
	pt := event cursorPoint.
	"prevent spurios mouse leave when dropping morphs"
	owner 
		morphsInFrontOf: self
		overlapping: (pt - 2 extent: 4 @ 4)
		do: [:m | m isHandMorph ifFalse: [(m fullContainsPoint: pt) ifTrue: [^self]]].
	self bounds forPoint: pt
		closestSideDistLen: 
			[:side :dist :len | 
			"Check for window side adjust"

			dist <= 2 ifTrue: [ptName := side]].
	ptName ifNil: 
			["Check for pane border adjust"

			^self spawnPaneFrameHandle: event].
	#(#topLeft #bottomRight #bottomLeft #topRight) do: 
			[:corner | 
			"Check for window corner adjust"

			(pt dist: (self bounds perform: corner)) < 20 ifTrue: [ptName := corner]].
	cursor := Cursor resizeForEdge: ptName.
	resizer := (OldNewHandleMorph new)
				sensorMode: self fastFramingOn;
				followHand: event hand
					forEachPointDo: 
						[:p | 
						localPt := self pointFromWorld: p.
						newBounds := self bounds 
									withSideOrCorner: ptName
									setToPoint: localPt
									minExtent: self minimumExtent.
						self fastFramingOn 
							ifTrue: 
								[Cursor currentCursor == cursor 
									ifFalse: 
										[(event hand)
											visible: false;
											refreshWorld;
											visible: true.
										cursor show].
								self doFastWindowReframe: ptName]
							ifFalse: 
								[self bounds: newBounds]]
					lastPointDo: [:p | ]
					withCursor: cursor.
	event hand world addMorphInLayer: resizer.
	resizer startStepping! !
	"Bring me to the front and make me able to respond to mouse and keyboard"

	| oldTop |
	self owner 
		ifNil: [^self	"avoid spurious activate when drop in trash"].
	oldTop := TopWindow.
	TopWindow := self.
	oldTop ifNotNil: [oldTop passivate].
	self owner firstSubmorph == self 
		ifFalse: [
			"Bring me (with any flex) to the top if not already"
			self owner addMorphFront: self].
	labelArea ifNotNil:  [
			self setStripeColorsFrom: self paneColorToUse].
	self isCollapsed 
		ifFalse: [
			model modelWakeUpIn: self.
			self positionSubmorphs.
			labelArea ifNil: [self adjustBorderUponActivationWhenLabeless]]! !
	"Answer a table defining default values for all the preferences in the release.  Returns a list of (pref-symbol, boolean-symbol) pairs"

	^  #(
		(abbreviatedBrowserButtons false)
		(alternativeBrowseIt false)
		(alternativeScrollbarLook true)
		(alternativeWindowLook true)
		(annotationPanes false)
		(automaticFlapLayout true)
		(automaticPlatformSettings true)
		(balloonHelpEnabled true)
		(browseWithPrettyPrint false)
		(browserShowsPackagePane false)
		(canRecordWhilePlaying false)
		(caseSensitiveFinds false)
		(changeSetVersionNumbers true)
		(checkForSlips true)
		(classicNewMorphMenu false)
		(cmdDotEnabled true)
		(collapseWindowsInPlace false)
		(colorWhenPrettyPrinting false)
		(confirmFirstUseOfStyle true)
		(conversionMethodsAtFileOut false)
		(cpuWatcherEnabled false)
		(debugHaloHandle true)
		(debugPrintSpaceLog false)
		(debugShowDamage false)
		(decorateBrowserButtons true)
		(diffsInChangeList true)
		(diffsWithPrettyPrint false)
		(dismissAllOnOptionClose false)
		(fastDragWindowForMorphic true)
		(fullScreenLeavesDeskMargins true)
		(hiddenScrollBars false)
		(higherPerformance false)
		(honorDesktopCmdKeys true)
		(ignoreStyleIfOnlyBold true)
		(inboardScrollbars true)
		(logDebuggerStackToFile true)
		(menuButtonInToolPane false)
		(menuColorFromWorld false)
		(menuKeyboardControl false)  
		(modalColorPickers true)
		(noviceMode false)
		(optionalButtons true)
		(personalizedWorldMenu true)
		(projectsSentToDisk false)
		(propertySheetFromHalo false)
		(restartAlsoProceeds false)
		(reverseWindowStagger true)
		(scrollBarsNarrow false)
		(scrollBarsWithoutMenuButton false)
		(selectiveHalos false)
		(showBoundsInHalo false)
		(simpleMenus false)
		(smartUpdating true)
		(soundQuickStart false)
		(soundStopWhenDone false)
		(soundsEnabled true)
		(systemWindowEmbedOK false)
		(thoroughSenders true)
		(twentyFourHourFileStamps true)
		(uniqueNamesInHalos false)
		(warnIfNoChangesFile true)
		(warnIfNoSourcesFile true))


"
Preferences defaultValueTableForCurrentRelease do:
	[:pair | (Preferences preferenceAt: pair first ifAbsent: [nil]) ifNotNilDo:
			[:pref | pref defaultValue: (pair last == #true)]].
Preferences chooseInitialSettings.
"! !
	"The classic bright Squeak look.  Windows have saturated colors and relatively low contrast; scroll-bars are of the flop-out variety and are on the left.  Many power-user features are enabled."

	self setPreferencesFrom:
	#(
		(alternativeScrollbarLook false)
		(alternativeWindowLook false)
		(annotationPanes true)
		(automaticFlapLayout true)
		(balloonHelpEnabled true)
		(browseWithPrettyPrint false)
		(browserShowsPackagePane false)
		(classicNewMorphMenu false)
		(cmdDotEnabled true)
		(collapseWindowsInPlace false)
		(colorWhenPrettyPrinting false)
		(debugHaloHandle true)
		(debugPrintSpaceLog false)
		(debugShowDamage false)
		(decorateBrowserButtons true)
		(diffsInChangeList true)
		(diffsWithPrettyPrint false)
		(fastDragWindowForMorphic true)
		(fullScreenLeavesDeskMargins true)
		(hiddenScrollBars false)
		(ignoreStyleIfOnlyBold true)
		(inboardScrollbars false)
		(logDebuggerStackToFile true)
		(menuButtonInToolPane false)
		(menuColorFromWorld false)
		(menuKeyboardControl true)
		(noviceMode false)
		(optionalButtons true)
		(personalizedWorldMenu true)
		(propertySheetFromHalo false)
		(restartAlsoProceeds false)
		(reverseWindowStagger true)
		(scrollBarsNarrow false)
		(scrollBarsWithoutMenuButton false)
		(selectiveHalos false)
		(simpleMenus false)
		(smartUpdating true)
		(systemWindowEmbedOK false)
		(thoroughSenders true)
		(warnIfNoChangesFile true)
		(warnIfNoSourcesFile true))! !

	self setPreferencesFrom:

	#(	
		(alternativeScrollbarLook true)
		(alternativeWindowLook true)
		(annotationPanes true)
		(balloonHelpEnabled false)
		(browseWithPrettyPrint false)
		(browserShowsPackagePane false)
		(caseSensitiveFinds true)
		(checkForSlips true)
		(cmdDotEnabled true)
		(collapseWindowsInPlace false)
		(colorWhenPrettyPrinting false)
		(diffsInChangeList true)
		(diffsWithPrettyPrint false)
		(fastDragWindowForMorphic true)
		(honorDesktopCmdKeys false)
		(ignoreStyleIfOnlyBold true)
		(inboardScrollbars true)
		(menuColorFromWorld false)
		(menuKeyboardControl true)
		(noviceMode false)
		(optionalButtons true)
		(personalizedWorldMenu false)
		(restartAlsoProceeds false)
		(scrollBarsNarrow true)
		(scrollBarsWithoutMenuButton false)
		(simpleMenus false)
		(smartUpdating true)
		(thoroughSenders true)
	"Similar to the brightSqueak theme, but with a number of idiosyncratic personal settings.   Note that caseSensitiveFinds is true"


	self setPreferencesFrom:
	#(
		(abbreviatedBrowserButtons false)
		(accessOnlineModuleRepositories noOpinion)
		(alternativeBrowseIt noOpinion)
		(alternativeScrollbarLook false)
		(alternativeWindowLook false)
		(annotationPanes true)
		(automaticFlapLayout true)
		(automaticPlatformSettings noOpinion)
		(balloonHelpEnabled true)
		(browseWithPrettyPrint false)
		(browserShowsPackagePane false)
		(canRecordWhilePlaying noOpinion)
		(caseSensitiveFinds true)
		(changeSetVersionNumbers true)
		(checkForSlips true)
		(classicNewMorphMenu false)
		(cmdDotEnabled true)
		(collapseWindowsInPlace false)
		(colorWhenPrettyPrinting false)
		(confirmFirstUseOfStyle true)
		(conservativeModuleDeActivation noOpinion)
		(conversionMethodsAtFileOut true)
		(cpuWatcherEnabled noOpinion)
		(debugHaloHandle true)
		(debugPrintSpaceLog true)
		(debugShowDamage false)
		(decorateBrowserButtons true)
		(diffsInChangeList true)
		(diffsWithPrettyPrint false)
		(dismissAllOnOptionClose true)
		(duplicateControlAndAltKeys false)
		(extraDebuggerButtons true)
		(fastDragWindowForMorphic true)
		(fullScreenLeavesDeskMargins true)
		(hiddenScrollBars false)
		(higherPerformance noOpinion)
		(honorDesktopCmdKeys true)
		(ignoreStyleIfOnlyBold true)
		(inboardScrollbars false)
		(lenientScopeForGlobals noOpinion)
		(logDebuggerStackToFile true)
		(menuButtonInToolPane false)
		(menuColorFromWorld false)
		(menuKeyboardControl true)  
		(modalColorPickers true)
		(modularClassDefinitions noOpinion)
		(noviceMode false)
		(optionalButtons true)
		(personalizedWorldMenu true)
		(projectsSentToDisk noOpinion)
		(propertySheetFromHalo false)
		(restartAlsoProceeds false)
		(reverseWindowStagger true)
		(scrollBarsNarrow false)
		(scrollBarsWithoutMenuButton false)
		(selectiveHalos false)
		(showBoundsInHalo false)
		(simpleMenus false)
		(smartUpdating true)
		(soundQuickStart noOpinion)
		(soundsEnabled true)
		(soundStopWhenDone noOpinion)
		(strongModules noOpinion)
		(swapControlAndAltKeys noOpinion)
		(swapMouseButtons  noOpinion)
		(systemWindowEmbedOK false)
		(thoroughSenders true)
		(twentyFourHourFileStamps false)
		(uniqueNamesInHalos false)
		(warnIfNoChangesFile true)
		(warnIfNoSourcesFile true))! !

	self setPreferencesFrom:

	#(	
		(alternativeScrollbarLook true)
		(alternativeWindowLook false)
		(annotationPanes false)
		(balloonHelpEnabled false)
		(browseWithPrettyPrint false)
		(browserShowsPackagePane false)
		(caseSensitiveFinds true)
		(checkForSlips false)
		(cmdDotEnabled true)
		(collapseWindowsInPlace false)
		(colorWhenPrettyPrinting false)
		(diffsInChangeList false)
		(diffsWithPrettyPrint false)
		(fastDragWindowForMorphic true)
		(honorDesktopCmdKeys false)
		(ignoreStyleIfOnlyBold true)
		(inboardScrollbars true)
		(menuColorFromWorld false)
		(menuKeyboardControl false)
		(noviceMode false)
		(optionalButtons false)
		(personalizedWorldMenu false)
		(restartAlsoProceeds false)
		(scrollBarsNarrow false)
		(scrollBarsWithoutMenuButton false)
		(simpleMenus false)
		(smartUpdating false)
		(thoroughSenders false)
	"A traditional monochrome Smalltalk-80 look and feel, clean and austere, and lacking many features added to Squeak in recent years. Caution: this theme removes the standard Squeak flaps, turns off the 'smartUpdating' feature that keeps multiple browsers in synch, and much more."

	self setPreferencesFrom:

	#(	
		(alternativeScrollbarLook false)
		(alternativeWindowLook false)
		(annotationPanes false)
		(balloonHelpEnabled false)
		(browseWithPrettyPrint false)
		(browserShowsPackagePane false)
		(caseSensitiveFinds true)
		(checkForSlips false)
		(cmdDotEnabled true)
		(collapseWindowsInPlace false)
		(colorWhenPrettyPrinting false)
		(diffsInChangeList false)
		(diffsWithPrettyPrint false)
		(fastDragWindowForMorphic true)
		(honorDesktopCmdKeys false)
		(ignoreStyleIfOnlyBold true)
		(inboardScrollbars false)
		(menuColorFromWorld false)
		(menuKeyboardControl false)
		(noviceMode false)
		(optionalButtons false)
		(personalizedWorldMenu false)
		(restartAlsoProceeds false)
		(scrollBarsNarrow false)
		(scrollBarsWithoutMenuButton false)
		(simpleMenus false)
		(smartUpdating false)
		(thoroughSenders false)