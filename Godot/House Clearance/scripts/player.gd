extends CharacterBody2D

const SPEED = 100.0
const JUMP_VELOCITY = -250.0
const SLIDE_FRICTION = 1.0

# Get the gravity from the project settings to be synced with RigidBody nodes.
var gravity = ProjectSettings.get_setting("physics/2d/default_gravity")
var queue_sliding = false;
var previous_direction = 0
var wait_on_input_release = false

enum MoveState { IDLE, MOVE, FALL, SLIDE, COVER, DEAD_DMG, DEAD_FALL }
var move_state:MoveState = MoveState.IDLE

func slide_player():
	velocity.x = move_toward(velocity.x, 0, SLIDE_FRICTION)
	if is_on_floor():
		$AnimatedSprite2D.animation = "slide"
		move_state = MoveState.SLIDE

func move_player(direction):
	velocity.x = direction * SPEED
	if sign(direction) != previous_direction && abs(direction) > 0.0001:
		if direction < 0:
			$AnimatedSprite2D.flip_h = true
		else:
			$AnimatedSprite2D.flip_h = false
	if is_on_floor():
		$AnimatedSprite2D.animation = "run"
	move_state = MoveState.MOVE

func stop_player():
	velocity.x = move_toward(velocity.x, 0, SPEED)
	if is_on_floor():
		$AnimatedSprite2D.animation = "idle"
	if move_state != MoveState.COVER:
		move_state = MoveState.IDLE
	else:
		queue_sliding = false
	#var viewport = get_viewport().get_viewport_rid()	
	#RenderingServer.viewport_set_snap_2d_transforms_to_pixel(viewport, true)
	#print("on")

func jump_player():
	velocity.y = JUMP_VELOCITY
	player_in_air()
	
func player_in_air():
	move_state = MoveState.FALL
	$AnimatedSprite2D.animation = "jump"
	
func _physics_process(delta):
	#var viewport = get_viewport().get_viewport_rid()
	#RenderingServer.viewport_set_snap_2d_transforms_to_pixel(viewport, false)
	#print("off")
	
	# Add the gravity.
	if not is_on_floor():
		velocity.y += gravity * delta
		player_in_air()

	# Handle jump.
	if Input.is_action_just_pressed("ui_accept") and is_on_floor():
		jump_player()

	# Get the input direction and handle the movement/deceleration.
	# As good practice, you should replace UI actions with custom gameplay actions.
	var direction = Input.get_axis("ui_left", "ui_right")
	
	# Handle in-cover
	if wait_on_input_release:
		if abs(direction) < 0.0001:
			wait_on_input_release = false
		else:
			direction = 0
	
	var slide_pressed = Input.is_action_pressed("ui_down")
	
	if slide_pressed:
		queue_sliding = true
		
	if queue_sliding or move_state == MoveState.SLIDE:
		if direction:
			if sign(direction) != sign(velocity.x):
				queue_sliding = false
				move_player(direction)
			else:
				slide_player()
		else:
			slide_player()
	else:
		if move_state != MoveState.SLIDE:
			if abs(direction) > 0.001:
				move_player(direction)
			else:
				stop_player()
	
	if abs(velocity.x) < 1.1 and move_state == MoveState.SLIDE:
		move_state = MoveState.IDLE
		stop_player()
	
	previous_direction = round(direction)
	
	# update sprite
	if move_state == MoveState.COVER:
		$AnimatedSprite2D.modulate = Color(0.5,0.5,0.5)
		$AnimatedSprite2D.position.y = -20
	else:
		$AnimatedSprite2D.modulate = Color(1,1,1)
		$AnimatedSprite2D.position.y = -16
		
	move_and_slide()
