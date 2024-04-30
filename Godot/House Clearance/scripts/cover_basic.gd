extends Area2D

@onready var player = %player
enum MoveState { IDLE, MOVE, FALL, SLIDE, COVER, DEAD_DMG, DEAD_FALL }

func _on_body_entered(body):
	print("entered body" +str(player) +", " +str(player.move_state))
	if player.move_state == MoveState.SLIDE:
		player.move_state = MoveState.COVER
		player.wait_on_input_release = true
		player.stop_player()
		player.velocity.x = 0
