extends AnimatedSprite2D

@export var pixelGridResoltionX : float = 64
@export var units = 100
# Called when the node enters the scene tree for the first time.
func _ready():
	
	pass # Replace with function body.


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta):
	var oldX = get_parent().global_position.x 
	var newX = floor(oldX/ units * pixelGridResoltionX) / (pixelGridResoltionX)
	newX = newX * units
	print(str(oldX) + " > " + str(newX))
	transform.x[0] = oldX - newX
