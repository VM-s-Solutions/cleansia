package cz.cleansia.core.ui

import androidx.compose.animation.AnimatedContentTransitionScope
import androidx.compose.animation.EnterTransition
import androidx.compose.animation.ExitTransition
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.navigation.NavBackStackEntry

/** How long a screen takes to slide in or out, in both apps. */
const val PUSH_TRANSITION_MILLIS = 280

/** A pushed screen slides in from the right while the one under it slides out to the left, both fading. */
val pushEnter: AnimatedContentTransitionScope<NavBackStackEntry>.() -> EnterTransition = {
    slideIntoContainer(
        AnimatedContentTransitionScope.SlideDirection.Left,
        animationSpec = tween(PUSH_TRANSITION_MILLIS),
    ) + fadeIn(tween(PUSH_TRANSITION_MILLIS))
}
val pushExit: AnimatedContentTransitionScope<NavBackStackEntry>.() -> ExitTransition = {
    slideOutOfContainer(
        AnimatedContentTransitionScope.SlideDirection.Left,
        animationSpec = tween(PUSH_TRANSITION_MILLIS),
    ) + fadeOut(tween(PUSH_TRANSITION_MILLIS))
}

/** Back undoes a push: the screen under it slides back in from the left, the popped one out to the right. */
val popEnter: AnimatedContentTransitionScope<NavBackStackEntry>.() -> EnterTransition = {
    slideIntoContainer(
        AnimatedContentTransitionScope.SlideDirection.Right,
        animationSpec = tween(PUSH_TRANSITION_MILLIS),
    ) + fadeIn(tween(PUSH_TRANSITION_MILLIS))
}
val popExit: AnimatedContentTransitionScope<NavBackStackEntry>.() -> ExitTransition = {
    slideOutOfContainer(
        AnimatedContentTransitionScope.SlideDirection.Right,
        animationSpec = tween(PUSH_TRANSITION_MILLIS),
    ) + fadeOut(tween(PUSH_TRANSITION_MILLIS))
}

/** The tab shell fades in place, under a push and back to it, instead of sliding. */
val fadeEnter: AnimatedContentTransitionScope<NavBackStackEntry>.() -> EnterTransition = {
    fadeIn(tween(PUSH_TRANSITION_MILLIS))
}
val fadeExit: AnimatedContentTransitionScope<NavBackStackEntry>.() -> ExitTransition = {
    fadeOut(tween(PUSH_TRANSITION_MILLIS))
}

/** The splash fades in and out, more slowly than a push. */
val fadeEnterLong: AnimatedContentTransitionScope<NavBackStackEntry>.() -> EnterTransition = {
    fadeIn(tween(400))
}
val fadeExitLong: AnimatedContentTransitionScope<NavBackStackEntry>.() -> ExitTransition = {
    fadeOut(tween(400))
}
